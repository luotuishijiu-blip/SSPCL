using System.IO.Compression;
using System.Text.Json;
using Sspcl.Core.Install;
using Sspcl.Core.Mods;
using Sspcl.Core.Versioning;

namespace Sspcl.Core.Modpack;

public sealed class PackImportResult
{
    public string Path { get; set; } = "";
    public PackManifest Manifest { get; set; } = new();
    public List<PackModReference> MissingMods { get; set; } = new();
}
public static class ModpackImporter
{
    public static PackManifest Inspect(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        return Inspect(zip);
    }
    private static PackManifest Inspect(ZipArchive zip)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long size = 0;
        if (zip.Entries.Count > 20000) throw new InvalidDataException("整合包文件过多。");
        foreach (var entry in zip.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            if (path.StartsWith("/") || path.Contains(":") || path.Split('/').Any(p => p == ".." || p == ".") || !names.Add(path))
                throw new InvalidDataException("整合包包含不安全或重复路径。");
            if (path.EndsWith("/")) continue;
            if (path != "pack.json" && !PackFiles.IsSetting(path) && !PackFiles.IsSave(path))
                throw new InvalidDataException("整合包禁止携带 MOD、游戏或其他文件：" + path);
            size = checked(size + entry.Length);
            if (size > 4L * 1024 * 1024 * 1024 || (path.StartsWith("settings/") && entry.Length > 16 * 1024 * 1024))
                throw new InvalidDataException("整合包内容超过大小限制。");
        }
        var manifestEntry = zip.GetEntry("pack.json") ?? throw new InvalidDataException("缺少 pack.json，不是 SSPCL 清单整合包。");
        if (manifestEntry.Length > 4 * 1024 * 1024) throw new InvalidDataException("整合包清单过大。");
        using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<PackManifest>(stream) ?? throw new InvalidDataException("清单为空。");
        if (manifest.Format != "sspcl-list-pack" || manifest.FormatVersion != 1 ||
            (!string.IsNullOrWhiteSpace(manifest.GameVersion) && GameVersion.Parse(manifest.GameVersion) == GameVersion.Unknown) ||
            (manifest.Mods != null && manifest.Mods.Any(m => m == null || string.IsNullOrWhiteSpace(m.Id))))
            throw new InvalidDataException("不支持的整合包格式或无效 MOD 清单。");
        if ((!manifest.HasSettings && zip.Entries.Any(e => e.FullName.Replace('\\', '/').StartsWith("settings/"))) ||
            (!manifest.HasSaves && zip.Entries.Any(e => e.FullName.Replace('\\', '/').StartsWith("saves/"))))
            throw new InvalidDataException("内容与整合包清单不一致。");
        return manifest;
    }

    public static PackImportResult Import(string archive, string baseGame, string destination, string modPool,
        ExportOptions selections, CancellationToken token = default)
    {
        // 验证与读取使用同一个只读文件句柄，防止验证后文件被替换。
        using var archiveStream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var manifest = Inspect(zip);
        var installation = InstallationDetector.Detect(baseGame);
        if (!installation.IsValid) throw new InvalidOperationException("请选择本机有效的游戏作为基础版本。");
        if (selections.IncludeGameVersion && !string.IsNullOrWhiteSpace(manifest.GameVersion) &&
            GameVersion.Parse(manifest.GameVersion) != installation.ParsedVersion)
            throw new InvalidOperationException("整合包需要 " + manifest.GameVersion + "，基础游戏为 " + installation.Version + "，请选择对应游戏版本。");
        var target = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
        var source = Path.GetFullPath(baseGame).TrimEnd(Path.DirectorySeparatorChar);
        if (Directory.Exists(target) || File.Exists(target) || target.Equals(source, StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("导入目标必须是基础游戏目录之外的新文件夹。");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string staging = Path.Combine(Path.GetDirectoryName(target)!, ".sspcl-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var gameFiles = Directory.EnumerateFiles(source).Concat(Directory.EnumerateDirectories(source)
                .Where(directory => !new[] { "mods", "saves", "Sspcl", "PCL", "ModPool" }.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase))
                .SelectMany(PackFiles.Enumerate));
            foreach (var file in gameFiles)
            {
                token.ThrowIfCancellationRequested();
                string relative = PackFiles.Relative(source, file);
                string first = relative.Split('/')[0];
                if (new[] { "mods", "saves", "Sspcl", "PCL", "ModPool" }.Contains(first, StringComparer.OrdinalIgnoreCase) || relative == ".sspcl-pack.json") continue;
                CopyLocal(file, Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)), token);
            }
            var result = new PackImportResult { Path = target, Manifest = new PackManifest {
                Name = selections.IncludePackName ? manifest.Name : null,
                GameVersion = selections.IncludeGameVersion ? manifest.GameVersion : null,
                Mods = selections.IncludeModList ? manifest.Mods : null,
                HasSettings = selections.IncludeGameSettings && manifest.HasSettings,
                HasSaves = selections.IncludeSaves && manifest.HasSaves
            }};
            {
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string path = entry.FullName.Replace('\\', '/');
                    if (path.EndsWith("/")) continue;
                    string? relative = null;
                    if (result.Manifest.HasSettings)
                    {
                        if (path == "settings/settings.json") relative = "starsector-core/data/config/settings.json";
                        else if (path.StartsWith("settings/common/")) relative = "saves/common/" + path.Substring("settings/common/".Length);
                    }
                    if (result.Manifest.HasSaves && path.StartsWith("saves/")) relative = path;
                    if (relative == null) continue;
                    var output = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    using var input = entry.Open();
                    using var outputStream = File.Create(output);
                    PackFiles.Copy(input, outputStream, token);
                }
            }
            var installed = new List<string>();
            if (result.Manifest.Mods != null)
            {
                var available = ModScanner.Scan(Path.Combine(source, "mods")).Concat(ModScanner.Scan(modPool)).ToList();
                foreach (var required in result.Manifest.Mods)
                {
                    var found = available.FirstOrDefault(m => m.Id == required.Id &&
                        (string.IsNullOrWhiteSpace(required.Version) || m.VersionRaw == required.Version));
                    if (found == null) { result.MissingMods.Add(required); continue; }
                    foreach (var file in PackFiles.Enumerate(found.Path))
                        CopyLocal(file, Path.Combine(staging, "mods", found.Folder, PackFiles.Relative(found.Path, file)), token);
                    installed.Add(required.Id);
                }
            }
            EnabledModsFile.Write(staging, installed);
            Directory.CreateDirectory(Path.Combine(staging, "saves"));
            File.WriteAllText(Path.Combine(staging, ".sspcl-pack.json"), JsonSerializer.Serialize(result.Manifest, ModpackExporter.JsonOptions));
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, target);
            return result;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    private static void CopyLocal(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("不支持链接文件。");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = File.OpenRead(source);
        using var output = File.Create(destination);
        PackFiles.Copy(input, output, token);
    }
    public static PackManifest? ReadInstalledManifest(string game)
    {
        try { return JsonSerializer.Deserialize<PackManifest>(File.ReadAllText(Path.Combine(game, ".sspcl-pack.json"))); }
        catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException) { return null; }
    }
}
