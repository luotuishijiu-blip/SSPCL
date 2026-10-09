using System.Text.Json.Nodes;
using Sspcl.Core.Mods;

namespace Sspcl.Core.Loadouts;

/// <summary>每次重载形成不可变使用的目录快照，只读当前启用 MOD，后加载资源覆盖前者。</summary>
public static class LoadoutCatalogReader
{
    private sealed class Root
    {
        public string Path { get; }
        public string Name { get; }
        public Root(string path, string name) { Path = path; Name = name; }
    }
    public static LoadoutCatalog Read(string gamePath, CancellationToken token = default)
    {
        var catalog = new LoadoutCatalog { GamePath = Path.GetFullPath(gamePath) };
        var roots = new List<Root> { new(Path.Combine(catalog.GamePath, "starsector-core"), "原版") };
        var mods = ModScanner.Scan(Path.Combine(gamePath, "mods")).Where(m => m.Id.Length > 0).GroupBy(m => m.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (string id in EnabledModsFile.Read(gamePath).Distinct(StringComparer.Ordinal))
            if (mods.TryGetValue(id, out var mod)) roots.Add(new Root(mod.Path, mod.Name));
        var hullStats = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var weaponStats = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var hullFiles = new Dictionary<string, (JsonObject Json, string File, string Source)>(StringComparer.Ordinal);
        var skinFiles = new Dictionary<string, (JsonObject Json, string File, string Source)>(StringComparer.Ordinal);
        var weaponFiles = new Dictionary<string, (JsonObject Json, string File, string Source)>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            token.ThrowIfCancellationRequested();
            MergeStats(Path.Combine(root.Path, "data", "hulls", "ship_data.csv"), hullStats);
            MergeStats(Path.Combine(root.Path, "data", "weapons", "weapon_data.csv"), weaponStats);
            ReadDefinitions(root, "data/hulls", ".ship", "hullId", hullFiles);
            ReadDefinitions(root, "data/hulls", ".skin", "skinHullId", skinFiles);
            ReadDefinitions(root, "data/weapons", ".wpn", "id", weaponFiles);
        }
        var hulls = new Dictionary<string, HullDefinition>(StringComparer.Ordinal);
        foreach (var pair in hullFiles)
        {
            token.ThrowIfCancellationRequested();
            var data = pair.Value.Json;
            hullStats.TryGetValue(pair.Key, out var stats);
            stats ??= new Dictionary<string, string>();
            var hull = new HullDefinition {
                Id = pair.Key, Name = GameDataReader.Get(stats, "name"), Source = pair.Value.Source, DefinitionPath = pair.Value.File,
                Width = GameDataReader.Number(data["width"]), Height = GameDataReader.Number(data["height"]),
                CenterX = GameDataReader.Number(data["center"]?[0]), CenterY = GameDataReader.Number(data["center"]?[1]),
                HullSize = GameDataReader.Text(data["hullSize"]), Designation = GameDataReader.Get(stats, "designation"),
                OrdnancePoints = (int)GameDataReader.GetNumber(stats, "ordnance points"), FluxDissipation = GameDataReader.GetNumber(stats, "flux dissipation"),
                SpritePath = ResolveAsset(GameDataReader.Text(data["spriteName"]), roots),
                Slots = Slots(data["weaponSlots"]), BuiltInWeapons = WeaponMap(data["builtInWeapons"]), BuiltInHullMods = GameDataReader.Strings(data["builtInMods"]).ToList()
            };
            if (string.IsNullOrWhiteSpace(hull.Name)) hull.Name = GameDataReader.Text(data["hullName"], hull.Id);
            if (string.IsNullOrWhiteSpace(hull.Name)) hull.Name = hull.Id;
            foreach (var slot in hull.Slots) if (hull.BuiltInWeapons.ContainsKey(slot.Id)) slot.Locked = true;
            if (hull.Width <= 0 || hull.Height <= 0) { Warn("船体尺寸无效：" + pair.Key); continue; }
            hulls[pair.Key] = hull;
        }
        // 递归解析皮肤继承，支持 MOD 的皮肤基于其他皮肤；循环不进入。
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        HullDefinition? ResolveSkin(string id)
        {
            if (hulls.TryGetValue(id, out var existing)) return existing;
            if (!skinFiles.TryGetValue(id, out var skin) || !visiting.Add(id)) return null;
            try
            {
                string baseId = GameDataReader.Text(skin.Json["baseHullId"]);
                HullDefinition? basis = hulls.TryGetValue(baseId, out var direct) ? direct : ResolveSkin(baseId);
                if (basis == null) { Warn("皮肤缺少基础船体：" + id + " → " + baseId); return null; }
                hullStats.TryGetValue(id, out var stats);
                var slots = basis.Slots.Select(CloneSlot).ToList();
                var remove = new HashSet<string>(GameDataReader.Strings(skin.Json["removeWeaponSlots"]), StringComparer.Ordinal);
                slots.RemoveAll(s => remove.Contains(s.Id));
                foreach (var slot in Slots(skin.Json["weaponSlots"])) { slots.RemoveAll(s => s.Id == slot.Id); slots.Add(slot); }
                if (skin.Json["weaponSlotChanges"] is JsonObject changes)
                    foreach (var change in changes)
                    {
                        var slot = slots.FirstOrDefault(s => s.Id == change.Key);
                        if (slot != null && change.Value is JsonObject values) ReadSlotProperties(slot, values);
                    }
                var builtIns = new Dictionary<string, string>(basis.BuiltInWeapons, StringComparer.Ordinal);
                foreach (string removeId in GameDataReader.Strings(skin.Json["removeBuiltInWeapons"])) builtIns.Remove(removeId);
                foreach (var weapon in WeaponMap(skin.Json["builtInWeapons"])) builtIns[weapon.Key] = weapon.Value;
                foreach (var slot in slots) slot.Locked = builtIns.ContainsKey(slot.Id) || slot.Type == "BUILT_IN";
                var hullMods = basis.BuiltInHullMods.Except(GameDataReader.Strings(skin.Json["removeBuiltInMods"])).Concat(GameDataReader.Strings(skin.Json["builtInMods"])).Distinct().ToList();
                var hull = new HullDefinition {
                    Id = id, Name = GameDataReader.Text(skin.Json["hullName"], stats == null || string.IsNullOrWhiteSpace(GameDataReader.Get(stats, "name")) ? basis.Name : GameDataReader.Get(stats, "name")),
                    BaseHullId = baseId, Source = skin.Source, DefinitionPath = skin.File, Width = basis.Width, Height = basis.Height,
                    CenterX = basis.CenterX, CenterY = basis.CenterY, HullSize = basis.HullSize, Designation = basis.Designation,
                    OrdnancePoints = (int)GameDataReader.Number(skin.Json["ordnancePoints"], stats == null || !stats.ContainsKey("ordnance points") ? basis.OrdnancePoints : GameDataReader.GetNumber(stats, "ordnance points")),
                    FluxDissipation = GameDataReader.Number(skin.Json["fluxDissipation"], basis.FluxDissipation),
                    Slots = slots, BuiltInWeapons = builtIns, BuiltInHullMods = hullMods,
                    SpritePath = skin.Json["spriteName"] == null ? basis.SpritePath : ResolveAsset(GameDataReader.Text(skin.Json["spriteName"]), roots)
                };
                hulls[id] = hull;
                if (string.IsNullOrWhiteSpace(hull.Name)) hull.Name = hull.Id;
                return hull;
            }
            finally { visiting.Remove(id); }
        }
        foreach (string id in skinFiles.Keys) { token.ThrowIfCancellationRequested(); ResolveSkin(id); }
        catalog.Hulls.AddRange(hulls.Values.Where(h => h.Slots.Count > 0 && (h.OrdnancePoints > 0 || h.Slots.Any(s => s.CanEquip))).OrderBy(h => h.Name, StringComparer.CurrentCulture));
        foreach (var pair in weaponFiles)
        {
            token.ThrowIfCancellationRequested();
            bool listed = weaponStats.TryGetValue(pair.Key, out var stats);
            stats ??= new Dictionary<string, string>();
            var spec = pair.Value.Json;
            string hints = string.Join(",", GameDataReader.Strings(spec["hints"])) + "," + GameDataReader.Get(stats, "hints");
            string tags = GameDataReader.Get(stats, "tags");
            var weapon = new WeaponDefinition {
                Id = pair.Key, Name = GameDataReader.Get(stats, "name"), Source = pair.Value.Source,
                Type = GameDataReader.Text(spec["type"]).ToUpperInvariant(), Size = GameDataReader.Text(spec["size"]).ToUpperInvariant(),
                OrdnancePoints = (int)GameDataReader.GetNumber(stats, "OPs"), Range = GameDataReader.GetNumber(stats, "range"),
                Dps = GameDataReader.GetNumber(stats, "damage/second"), DamageType = GameDataReader.Get(stats, "type"),
                FluxPerSecond = GameDataReader.GetNumber(stats, "energy/second"), PointDefense = hints.Split(',').Any(h => h.Trim() == "PD" || h.Trim() == "PD_ONLY"),
                FluxPerShot = GameDataReader.GetNumber(stats, "energy/shot"), AmmoCapacity = GameDataReader.GetNumber(stats, "ammo"), AmmoRegeneration = GameDataReader.GetNumber(stats, "ammo/sec"),
                Restricted = !listed || hints.Split(',').Any(h => h.Trim() == "SYSTEM") || tags.Split(',').Any(t => t.Trim() == "restricted"),
                Recommendable = !hints.Split(',').Any(h => h.Trim() == "BOMB") && !(tags.Split(',').Any(t => t.Trim() == "no_drop") && tags.Split(',').Any(t => t.Trim() == "no_sell")),
                TurretSprite = ResolveAsset(GameDataReader.Text(spec["turretSprite"]), roots), HardpointSprite = ResolveAsset(GameDataReader.Text(spec["hardpointSprite"]), roots),
                TurretGunSprite = ResolveAsset(GameDataReader.Text(spec["turretGunSprite"]), roots), HardpointGunSprite = ResolveAsset(GameDataReader.Text(spec["hardpointGunSprite"]), roots),
                BarrelBelow = GameDataReader.Strings(spec["renderHints"]).Contains("RENDER_BARREL_BELOW"),
                AnimationFrames = Math.Max(1, (int)GameDataReader.Number(spec["numFrames"], 1)), AnimateHorizontal = GameDataReader.Text(spec["frameDisplayHeight"]) == ""
            };
            if (weapon.Dps <= 0)
            {
                double cycle = GameDataReader.GetNumber(stats, "chargeup") + GameDataReader.GetNumber(stats, "chargedown") + GameDataReader.GetNumber(stats, "burst delay") * Math.Max(0, GameDataReader.GetNumber(stats, "burst size") - 1);
                if (cycle > 0) weapon.Dps = GameDataReader.GetNumber(stats, "damage/shot") * Math.Max(1, GameDataReader.GetNumber(stats, "burst size")) / cycle;
            }
            if (weapon.FluxPerSecond <= 0 && weapon.Dps > 0 && GameDataReader.GetNumber(stats, "damage/shot") > 0)
                weapon.FluxPerSecond = weapon.Dps / GameDataReader.GetNumber(stats, "damage/shot") * GameDataReader.GetNumber(stats, "energy/shot");
            if (weapon.Name.Length == 0) weapon.Name = weapon.Id;
            catalog.Weapons.Add(weapon);
        }
        catalog.Weapons.Sort((a, b) => StringComparer.CurrentCulture.Compare(a.Name, b.Name));
        return catalog;

        void Warn(string message) { if (catalog.Warnings.Count < 100) catalog.Warnings.Add(message); }
        void ReadDefinitions(Root root, string relative, string extension, string idKey, Dictionary<string, (JsonObject Json, string File, string Source)> destination)
        {
            string directory = Path.Combine(root.Path, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.EnumerateFiles(directory, "*" + extension, SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var json = GameDataReader.Json(file);
                    if (extension == ".ship" && (json["center"] is not JsonArray center || center.Count < 2))
                    { Warn(Path.GetFileName(file) + "：船体 center 必须包含两个坐标。"); continue; }
                    string id = GameDataReader.Text(json[idKey]);
                    if (id.Length > 0) destination[id] = (json, file, root.Name);
                }
                catch (Exception ex) when (ex is IOException || ex is System.Text.Json.JsonException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is FormatException || ex is OverflowException)
                { Warn(Path.GetFileName(file) + "：" + ex.Message); }
            }
        }
    }
    private static void MergeStats(string path, Dictionary<string, Dictionary<string, string>> destination)
    {
        foreach (var row in GameDataReader.Csv(path))
        {
            string id = GameDataReader.Get(row, "id");
            if (!destination.TryGetValue(id, out var stats)) destination[id] = stats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row) if (!string.IsNullOrWhiteSpace(cell.Value)) stats[cell.Key] = cell.Value;
        }
    }
    private static string ResolveAsset(string relative, List<Root> roots)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return "";
        relative = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        foreach (var root in roots.AsEnumerable().Reverse())
        {
            string path = Path.GetFullPath(Path.Combine(root.Path, relative));
            if (path.StartsWith(Path.GetFullPath(root.Path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return path;
        }
        return "";
    }
    private static Dictionary<string, string> WeaponMap(JsonNode? node) => node is JsonObject map ? map.ToDictionary(p => p.Key, p => GameDataReader.Text(p.Value), StringComparer.Ordinal) : new(StringComparer.Ordinal);
    private static List<WeaponSlot> Slots(JsonNode? node)
    {
        var slots = new List<WeaponSlot>();
        if (node is not JsonArray array) return slots;
        foreach (var value in array.OfType<JsonObject>())
        {
            var slot = new WeaponSlot { Id = GameDataReader.Text(value["id"]) };
            ReadSlotProperties(slot, value);
            if (slot.Id.Length > 0) slots.Add(slot);
        }
        return slots;
    }
    private static void ReadSlotProperties(WeaponSlot slot, JsonObject value)
    {
        slot.Type = GameDataReader.Text(value["type"], slot.Type).ToUpperInvariant();
        slot.Size = GameDataReader.Text(value["size"], slot.Size).ToUpperInvariant();
        slot.Mount = GameDataReader.Text(value["mount"], slot.Mount).ToUpperInvariant();
        slot.Angle = GameDataReader.Number(value["angle"], slot.Angle); slot.Arc = GameDataReader.Number(value["arc"], slot.Arc);
        if (value["locations"] is JsonArray location && location.Count > 0)
        {
            if (location[0] is JsonArray nested) location = nested;
            if (location.Count >= 2) { slot.X = GameDataReader.Number(location[0]); slot.Y = GameDataReader.Number(location[1]); }
        }
        slot.Locked = slot.Type == "BUILT_IN" || slot.Type == "DECORATIVE" || slot.Type == "STATION_MODULE" || slot.Type == "LAUNCH_BAY";
    }
    private static WeaponSlot CloneSlot(WeaponSlot source) => new() { Id = source.Id, Type = source.Type, Size = source.Size, Mount = source.Mount, X = source.X, Y = source.Y, Angle = source.Angle, Arc = source.Arc, Locked = source.Locked };
}
