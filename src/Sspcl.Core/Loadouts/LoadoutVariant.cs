using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sspcl.Core.Loadouts;

public static class LoadoutVariant
{
    public static string Serialize(HullDefinition hull, IReadOnlyList<WeaponDefinition> weapons, LoadoutPlan plan, string variantId)
    {
        var evaluation = LoadoutRules.Evaluate(hull, weapons, plan);
        if (!evaluation.Valid) throw new InvalidOperationException(string.Join("\n", evaluation.Errors));
        if (hull.Slots.Any(s => s.Type == "STATION_MODULE")) throw new InvalidOperationException("模块舰船需要独立的子舰配置，当前无法导出完整配置。");
        if (string.IsNullOrWhiteSpace(variantId) || variantId.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
            throw new ArgumentException("配置 ID 只能包含字母、数字、下划线或短横线。", nameof(variantId));
        var groups = new JsonArray();
        var all = new Dictionary<string, string>(hull.BuiltInWeapons, StringComparer.Ordinal);
        foreach (var pair in plan.Weapons) all[pair.Key] = pair.Value;
        foreach (var group in all.GroupBy(pair => weapons.FirstOrDefault(w => w.Id == pair.Value)?.Type == "MISSILE" ? "LINKED" : "ALTERNATING"))
        {
            var assignments = new JsonObject();
            foreach (var assignment in group) assignments[assignment.Key] = assignment.Value;
            groups.Add(new JsonObject { ["autofire"] = true, ["mode"] = group.Key, ["weapons"] = assignments });
        }
        var root = new JsonObject {
            ["hullId"] = hull.Id, ["variantId"] = variantId, ["displayName"] = plan.Name,
            ["fluxVents"] = plan.Vents, ["fluxCapacitors"] = plan.Capacitors, ["weaponGroups"] = groups,
            ["hullMods"] = new JsonArray(), ["wings"] = new JsonArray(), ["quality"] = 1, ["goalVariant"] = false
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
    public static LoadoutPlan Read(string path, HullDefinition hull, IReadOnlyList<WeaponDefinition> weapons)
    {
        var data = GameDataReader.Json(path);
        var plan = new LoadoutPlan { HullId = GameDataReader.Text(data["hullId"]), Name = GameDataReader.Text(data["displayName"], "导入装配"), Vents = (int)GameDataReader.Number(data["fluxVents"]), Capacitors = (int)GameDataReader.Number(data["fluxCapacitors"]) };
        if (GameDataReader.Strings(data["hullMods"]).Except(hull.BuiltInHullMods).Any() ||
            GameDataReader.Strings(data["permaMods"]).Except(hull.BuiltInHullMods).Any() ||
            GameDataReader.Strings(data["sMods"]).Any() || GameDataReader.Strings(data["wings"]).Any())
            throw new InvalidDataException("此配置含额外船插或舰载机，目前的武器装配模块无法计算其 OP，不能直接导入。");
        if (data["modules"] is JsonObject modules && modules.Count > 0) throw new InvalidDataException("此配置含舰船模块，需要专用模块配置，不能直接导入。");
        if (data["weaponGroups"] is JsonArray groups)
            foreach (var group in groups.OfType<JsonObject>())
                if (group["weapons"] is JsonObject assignments)
                    foreach (var assignment in assignments)
                    {
                        if (hull.BuiltInWeapons.TryGetValue(assignment.Key, out string? builtin) && builtin == GameDataReader.Text(assignment.Value)) continue;
                        if (plan.Weapons.ContainsKey(assignment.Key)) throw new InvalidDataException("配置重复分配槽位：" + assignment.Key);
                        plan.Weapons[assignment.Key] = GameDataReader.Text(assignment.Value);
                        plan.PinnedSlots.Add(assignment.Key);
                    }
        var evaluation = LoadoutRules.Evaluate(hull, weapons, plan);
        if (!evaluation.Valid) throw new InvalidDataException(string.Join("\n", evaluation.Errors));
        return plan;
    }
}
