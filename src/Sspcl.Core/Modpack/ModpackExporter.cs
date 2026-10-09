using System.IO.Compression;
using System.Text.Json;
using Sspcl.Core.Install;
using Sspcl.Core.Mods;

namespace Sspcl.Core.Modpack;

public sealed class ExportOptions
{
    public bool IncludeModList { get; set; } = true;
    public bool IncludeGameSettings { get; set; } = true;
    public bool IncludeGameVersion { get; set; } = true;
    public bool IncludePackName { get; set; } = true;
    public bool IncludeSaves { get; set; }
}
public sealed class PackModReference
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
}
public sealed class PackManifest
{
    public int FormatVersion { get; set; } = 1;
    public string Format { get; set; } = "sspcl-list-pack";
    public string? Name { get; set; }
    public string? GameVersion { get; set; }
    public List<PackModReference>? Mods { get; set; }
    public bool HasSettings { get; set; }
    public bool HasSaves { get; set; }
}

/// <summary>仅分享清单、配置和存档；没有 MOD/游戏文件导出选项。</summary>
public static class ModpackExporter
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string Export(string installPath, string outZipPath, string packName, ExportOptions opts, CancellationToken token = default)
    {
        var installation = InstallationDetector.Detect(installPath);
        if (!installation.IsValid) throw new InvalidOperationException("请选择有效的游戏目录。");
        var manifest = new PackManifest { Name = opts.IncludePackName ? packName.Trim() : null,
            GameVersion = opts.IncludeGameVersion ? installation.Version : null,
            HasSettings = opts.IncludeGameSettings, HasSaves = opts.IncludeSaves };
        if (opts.IncludeGameVersion && string.IsNullOrWhiteSpace(installation.Version))
            throw new InvalidOperationException("无法识别游戏版本，请取消游戏版本选项或选择有效的游戏目录。");
        if (opts.IncludeModList)
        {
            var mods = ModScanner.Scan(Path.Combine(installPath, "mods"));
            manifest.Mods = EnabledModsFile.Read(installPath).Distinct().Select(id => {
                var mod = mods.FirstOrDefault(m => m.Id == id);
                return new PackModReference { Id = id, Name = mod?.Name ?? id, Version = mod?.VersionRaw ?? "" };
            }).ToList();
        }
        var destination = Path.GetFullPath(outZipPath);
        if (!new[] { ".zip", ".sspack" }.Contains(Path.GetExtension(destination).ToLowerInvariant()))
            throw new ArgumentException("整合包文件扩展名必须是 .sspack 或 .zip。", nameof(outZipPath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("pack.json").Open()))
                    writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
                if (opts.IncludeGameSettings)
                {
                    AddFile(zip, Path.Combine(installPath, "starsector-core/data/config/settings.json"), "settings/settings.json", token);
                    foreach (var folder in new[] { "config", "LunaSettings" })
                    {
                        string root = Path.Combine(installPath, "saves/common", folder);
                        if (!Directory.Exists(root)) continue;
                        foreach (var file in PackFiles.Enumerate(root))
                        {
                            string entry = "settings/common/" + folder + "/" + PackFiles.Relative(root, file);
                            if (PackFiles.IsSetting(entry)) AddFile(zip, file, entry, token);
                        }
                    }
                }
                if (opts.IncludeSaves && Directory.Exists(Path.Combine(installPath, "saves")))
                    foreach (var save in Directory.EnumerateDirectories(Path.Combine(installPath, "saves")))
                    {
                        if (!File.Exists(Path.Combine(save, "descriptor.xml"))) continue;
                        foreach (var file in PackFiles.Enumerate(save))
                        {
                            string entry = "saves/" + Path.GetFileName(save) + "/" + PackFiles.Relative(save, file);
                            if (!PackFiles.IsSave(entry)) throw new InvalidDataException("存档中含有不支持的文件：" + Path.GetFileName(file));
                            AddFile(zip, file, entry, token);
                        }
                    }
            }
            token.ThrowIfCancellationRequested();
            ModpackImporter.Inspect(temporary);
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void AddFile(ZipArchive zip, string file, string entry, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(file)) return;
        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("不支持链接文件。");
        using var input = File.OpenRead(file);
        using var output = zip.CreateEntry(entry, CompressionLevel.Optimal).Open();
        PackFiles.Copy(input, output, token);
    }
}

internal static class PackFiles
{
    internal static string Relative(string root, string file) => file.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
    internal static IEnumerable<string> Enumerate(string root)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("不支持链接目录。");
        foreach (var file in Directory.EnumerateFiles(root))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("不支持链接文件。");
            yield return file;
        }
        foreach (var directory in Directory.EnumerateDirectories(root))
            foreach (var file in Enumerate(directory)) yield return file;
    }
    internal static bool IsSetting(string path) => path == "settings/settings.json" ||
        ((path.StartsWith("settings/common/config/", StringComparison.Ordinal) || path.StartsWith("settings/common/LunaSettings/", StringComparison.Ordinal)) &&
         new[] { ".json", ".ini", ".cfg", ".xml", ".properties", ".txt" }.Contains(Path.GetExtension(path).ToLowerInvariant()));
    internal static bool IsSave(string path) => path.StartsWith("saves/", StringComparison.Ordinal) && path.Split('/').Length >= 3 &&
        !path.Split('/')[1].Equals("common", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(path).Equals("mod_info.json", StringComparison.OrdinalIgnoreCase) &&
        new[] { ".xml", ".xml.gz", ".xml.bak", ".xml.gz.bak", ".json" }.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    internal static void Copy(Stream input, Stream output, CancellationToken token)
    {
        var buffer = new byte[81920];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
    }
}
