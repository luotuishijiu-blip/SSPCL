using System.Text.Json;

namespace Sspcl.Core.Mods;

public sealed class ModProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> EnabledMods { get; set; } = new();
    public DateTime UpdatedUtc { get; set; }
    public string? LegacySource { get; set; }
}

/// <summary>方案目录只保存清单与可选整合包数据，MOD 文件统一留在游戏 mods。</summary>
public sealed class ModProfileStore
{
    public string Root { get; }
    public ModProfileStore(string root) { Root = Path.GetFullPath(root); }
    public string DirectoryOf(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("无效方案 ID。");
        return Path.Combine(Root, id);
    }
    public ModProfile Read(string id)
    {
        var profile = JsonSerializer.Deserialize<ModProfile>(File.ReadAllText(Path.Combine(DirectoryOf(id), "profile.json"))) ?? throw new InvalidDataException("方案为空。");
        if (profile.Id != id || profile.EnabledMods == null || string.IsNullOrWhiteSpace(profile.Name)) throw new InvalidDataException("方案清单无效。");
        return profile;
    }
    public List<ModProfile> List()
    {
        if (!Directory.Exists(Root)) return new();
        return Directory.GetDirectories(Root).Where(p => Guid.TryParseExact(Path.GetFileName(p), "N", out _))
            .Select(p => Read(Path.GetFileName(p))).OrderBy(p => p.Name, StringComparer.CurrentCulture).ToList();
    }
    public ModProfile Save(string name, IEnumerable<string> ids, string? replaceId = null, string? legacySource = null)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 80) throw new ArgumentException("方案名字需为 1～80 个字符。");
        var profile = replaceId == null ? new ModProfile { Id = Guid.NewGuid().ToString("N") } : Read(replaceId);
        profile.Name = name; profile.EnabledMods = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        profile.UpdatedUtc = DateTime.UtcNow; profile.LegacySource = legacySource ?? profile.LegacySource;
        string directory = DirectoryOf(profile.Id); Directory.CreateDirectory(directory);
        AtomicWrite(Path.Combine(directory, "profile.json"), JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        return profile;
    }
    internal static void AtomicWrite(string path, string text)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, text); if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static HashSet<string> ResolveSelection(IEnumerable<string> requested, IReadOnlyList<ModSpec> mods)
    {
        var result = new HashSet<string>(requested, StringComparer.Ordinal);
        result.UnionWith(DependencyResolver.Closure(result, mods));
        return result;
    }
    public static List<string> Missing(IEnumerable<string> ids, IReadOnlyList<ModSpec> mods)
    {
        var desired = ResolveSelection(ids, mods); var installed = new HashSet<string>(mods.Select(m => m.Id), StringComparer.Ordinal);
        return desired.Where(id => !installed.Contains(id)).Concat(mods.Where(m => desired.Contains(m.Id)).SelectMany(m => m.Dependencies).Select(d => d.Id).Where(id => !string.IsNullOrWhiteSpace(id) && !installed.Contains(id))).Distinct(StringComparer.Ordinal).ToList();
    }
    public void Apply(string id, string gameDir)
    {
        var profile = Read(id); var mods = ModScanner.Scan(Path.Combine(gameDir, "mods"));
        var missing = Missing(profile.EnabledMods, mods);
        if (missing.Count > 0) throw new InvalidOperationException("此方案缺少 MOD 或前置，请下载或重新保存选择：" + string.Join("、", missing));
        var desired = ResolveSelection(profile.EnabledMods, mods);
        var report = DependencyResolver.Analyze(mods.Where(m => desired.Contains(m.Id)).ToList(), Sspcl.Core.Versioning.GameVersion.Unknown);
        if (report.Duplicates.Count > 0) throw new InvalidOperationException("共享目录存在重复 MOD ID：" + string.Join("、", report.Duplicates.Keys));
        if (report.VersionMismatch.Count > 0) throw new InvalidOperationException("MOD 前置版本不足：" + string.Join("、", report.VersionMismatch.Select(m => m.DepId + " 需要 " + m.Required)));
        EnabledModsFile.Write(gameDir, ResolveSelection(profile.EnabledMods, mods));
    }
    public void MigrateLists(IEnumerable<string> oldGames)
    {
        var existing = List();
        foreach (string game in oldGames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(game) || existing.Any(p => string.Equals(p.LegacySource, Path.GetFullPath(game), StringComparison.OrdinalIgnoreCase))) continue;
            var profile = Save(Path.GetFileName(game.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), EnabledModsFile.Read(game), legacySource: Path.GetFullPath(game));
            existing.Add(profile);
        }
    }
    public static void MergeLegacyMods(string gameDir, IEnumerable<string> oldModsDirectories)
    {
        string target = Path.Combine(gameDir, "mods"); Directory.CreateDirectory(target);
        var installed = new HashSet<string>(ModScanner.Scan(target).Select(m => m.Id), StringComparer.Ordinal);
        foreach (string source in oldModsDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var mod in ModScanner.Scan(source).Where(m => !string.IsNullOrWhiteSpace(m.Id)))
            {
                if (installed.Contains(mod.Id)) continue;
                string destination = Path.Combine(target, mod.Folder);
                if (Directory.Exists(destination) || File.Exists(destination)) destination = Path.Combine(target, "import-" + Guid.NewGuid().ToString("N"));
                string stage = Path.Combine(target, ".sspcl-migrate-" + Guid.NewGuid().ToString("N"));
                try { ModActivationService.CopyDirectory(mod.Path, stage); Directory.Move(stage, destination); installed.Add(mod.Id); }
                finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            }
    }
}
