using System.IO.Compression;
using Sspcl.Core.Mods;

namespace Sspcl.Core.Modpack;

public static class ProfilePackImporter
{
    public static ModProfile Import(string archive, ModProfileStore store, ExportOptions options, CancellationToken token = default)
    {
        using var stream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var manifest = ModpackImporter.Inspect(zip);
        Directory.CreateDirectory(store.Root);
        string stage = Path.Combine(store.Root, ".import-" + Guid.NewGuid().ToString("N"));
        string id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested(); string path = entry.FullName.Replace('\\', '/');
                if (path.EndsWith("/") || !(options.IncludeGameSettings && PackFiles.IsSetting(path) || options.IncludeSaves && PackFiles.IsSave(path))) continue;
                string destination = Path.Combine(stage, "payload", path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open(); using var output = File.Create(destination); PackFiles.Copy(input, output, token);
            }
            var profile = new ModProfile { Id = id, Name = options.IncludePackName && !string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Name! : "导入整合包", EnabledMods = options.IncludeModList ? manifest.Mods?.Select(m => m.Id).Distinct(StringComparer.Ordinal).ToList() ?? new() : new(), UpdatedUtc = DateTime.UtcNow };
            ModProfileStore.AtomicWrite(Path.Combine(stage, "profile.json"), System.Text.Json.JsonSerializer.Serialize(profile));
            token.ThrowIfCancellationRequested(); Directory.Move(stage, store.DirectoryOf(id)); return profile;
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
    public static void Apply(ModProfileStore store, string id, string gameDir)
    {
        var profile = store.Read(id);
        var missing = ModProfileStore.Missing(profile.EnabledMods, ModScanner.Scan(Path.Combine(gameDir, "mods")));
        if (missing.Count > 0) throw new InvalidOperationException("方案缺少 MOD 或前置：" + string.Join("、", missing));
        string payload = Path.Combine(store.DirectoryOf(id), "payload");
        var files = Directory.Exists(payload) ? PackFiles.Enumerate(payload).OrderBy(p => p, StringComparer.Ordinal).ToList() : new List<string>();
        string marker = Path.Combine(gameDir, "Sspcl", "ProfilePayloads", id + ".json");
        string stamp;
        using(var hash = System.Security.Cryptography.SHA256.Create())
        {
            var records = files.Where(f => PackFiles.IsSetting(PackFiles.Relative(payload, f))).Select(f => {
                using var stream = File.OpenRead(f);
                return PackFiles.Relative(payload, f) + ":" + Convert.ToBase64String(hash.ComputeHash(stream));
            });
            stamp = Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", records))));
        }
        bool settingsApplied = false;
        if(File.Exists(marker))
        {
            using var state = System.Text.Json.JsonDocument.Parse(File.ReadAllText(marker));
            settingsApplied = state.RootElement.TryGetProperty("stamp", out var saved) && saved.GetString() == stamp;
        }
        var existingSaves = Directory.Exists(Path.Combine(gameDir, "saves")) ? new HashSet<string>(Directory.GetDirectories(Path.Combine(gameDir, "saves")).Select(p => Path.GetFileName(p)!), StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = new List<(string Path, byte[]? Original)>();
        string backup = Path.Combine(gameDir, "Sspcl", "ProfileBackups", Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string file in files)
            {
                string relative = PackFiles.Relative(payload, file);
                string? target = relative == "settings/settings.json" ? "starsector-core/data/config/settings.json" :
                    PackFiles.IsSetting(relative) && relative.StartsWith("settings/common/") ? "saves/common/" + relative.Substring("settings/common/".Length) : PackFiles.IsSave(relative) ? relative : null;
                if (target == null) throw new InvalidDataException("方案包含不支持的内容：" + relative);
                if (settingsApplied && PackFiles.IsSetting(relative)) continue;
                string destination = Path.Combine(gameDir, target.Replace('/', Path.DirectorySeparatorChar));
                // 存档只首次导入，避免每次启动把玩家进度覆盖回分享时的内容。
                if (PackFiles.IsSave(relative) && existingSaves.Contains(relative.Split('/')[1])) continue;
                byte[]? original = File.Exists(destination) ? File.ReadAllBytes(destination) : null;
                if (original != null) { string saved = Path.Combine(backup, target.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(saved)!); File.WriteAllBytes(saved, original); }
                changed.Add((destination, original)); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination, true);
            }
            if(!settingsApplied && files.Any(f => PackFiles.IsSetting(PackFiles.Relative(payload, f))))
            {
                changed.Add((marker, File.Exists(marker) ? File.ReadAllBytes(marker) : null));
                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                ModProfileStore.AtomicWrite(marker, System.Text.Json.JsonSerializer.Serialize(new { stamp }));
            }
            store.Apply(id, gameDir);
        }
        catch
        {
            foreach (var item in changed.AsEnumerable().Reverse()) { if (item.Original == null) File.Delete(item.Path); else File.WriteAllBytes(item.Path, item.Original); }
            throw;
        }
    }
}
