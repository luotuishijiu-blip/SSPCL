using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sspcl.Core.Mods;

/// <summary>mods\enabled_mods.json 的读写（唯一真相来源）。写入走「备份 + 临时文件 + 原子替换」。</summary>
public static class EnabledModsFile
{
    public const string FileName = "enabled_mods.json";

    public static string GetPath(string gameDir) => Path.Combine(gameDir, "mods", FileName);

    public static List<string> Read(string gameDir)
    {
        var path = GetPath(gameDir);
        if (!File.Exists(path)) return new List<string>();
        try
        {
            var root = JsonSerializer.Deserialize<EnabledModsDocument>(File.ReadAllText(path));
            return root?.EnabledMods ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void Write(string gameDir, IEnumerable<string> ids, int maxBackups = 10)
    {
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);
        var path = GetPath(gameDir);

        var ordered = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        var json = JsonSerializer.Serialize(new EnabledModsDocument { EnabledMods = ordered },
            new JsonSerializerOptions { WriteIndented = true });

        if (File.Exists(path))
        {
            var backup = $"{path}.bak-{DateTime.Now:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}";
            File.Copy(path, backup);
            PruneBackups(path, maxBackups);
        }

        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    private static void PruneBackups(string path, int maxBackups)
    {
        var dir = Path.GetDirectoryName(path)!;
        var baseName = Path.GetFileName(path);
        var backups = Directory.GetFiles(dir, baseName + ".bak-*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        foreach (var file in backups.Skip(maxBackups))
        {
            try { File.Delete(file); } catch { /* 忽略清理失败 */ }
        }
    }

    /// <summary>列出备份（新的在前，返回完整路径）。</summary>
    public static List<string> ListBackups(string gameDir)
    {
        var dir = Path.Combine(gameDir, "mods");
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetFiles(dir, FileName + ".bak-*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    /// <summary>用指定备份覆盖当前 enabled_mods.json；覆盖前先把当前文件再做一份安全备份。</summary>
    public static void RestoreBackup(string backupPath, string gameDir)
    {
        var target = GetPath(gameDir);
        if (File.Exists(target))
        {
            var safety = $"{target}.pre-restore-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(target, safety);
        }
        File.Copy(backupPath, target, overwrite: true);
    }

    private sealed class EnabledModsDocument
    {
        [JsonPropertyName("enabledMods")]
        public List<string> EnabledMods { get; set; } = new();
    }
}
