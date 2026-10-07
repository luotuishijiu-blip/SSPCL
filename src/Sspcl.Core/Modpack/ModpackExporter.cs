using System.IO.Compression;
using System.Text.Json;
using Sspcl.Core.Mods;

namespace Sspcl.Core.Modpack;

public sealed class ExportOptions
{
    public bool IncludeEnabledMods { get; set; } = true;
    public bool IncludeDisabledMods { get; set; }
    public bool IncludeModSettings { get; set; } = true;
    public bool IncludeGameSettings { get; set; } = true;
    public bool IncludeSaves { get; set; }
}

/// <summary>把某个安装打包成可分享的整合包 zip（不含游戏本体）。</summary>
public static class ModpackExporter
{
    public static string Export(string installPath, string outZipPath, string packName, string packVersion, ExportOptions opts)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "sspcl-pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var enabled = new HashSet<string>(EnabledModsFile.Read(installPath), StringComparer.Ordinal);
            var mods = ModScanner.Scan(Path.Combine(installPath, "mods"));

            if (opts.IncludeEnabledMods || opts.IncludeDisabledMods)
            {
                var modsOut = Path.Combine(tmp, "mods");
                Directory.CreateDirectory(modsOut);
                foreach (var m in mods)
                {
                    bool isEnabled = enabled.Contains(m.Id);
                    if (isEnabled && opts.IncludeEnabledMods)
                        CopyDir(m.Path, Path.Combine(modsOut, m.Folder));
                    else if (!isEnabled && opts.IncludeDisabledMods)
                        CopyDir(m.Path, Path.Combine(modsOut, m.Folder));
                }
                if (opts.IncludeEnabledMods)
                {
                    var list = enabled.Where(id => mods.Any(m => m.Id == id)).ToList();
                    File.WriteAllText(Path.Combine(tmp, "enabled_mods.json"),
                        JsonSerializer.Serialize(new { enabledMods = list }, new JsonSerializerOptions { WriteIndented = true }));
                }
            }

            if (opts.IncludeGameSettings)
            {
                var vm = Path.Combine(installPath, "vmparams");
                if (File.Exists(vm)) File.Copy(vm, Path.Combine(tmp, "vmparams"));
            }

            if (opts.IncludeModSettings)
            {
                var cfg = Path.Combine(installPath, "saves", "common", "config");
                if (Directory.Exists(cfg)) CopyDir(cfg, Path.Combine(tmp, "mod_config"));
                var luna = Path.Combine(installPath, "saves", "common", "LunaSettings");
                if (Directory.Exists(luna)) CopyDir(luna, Path.Combine(tmp, "LunaSettings"));
            }

            if (opts.IncludeSaves)
            {
                var saves = Path.Combine(installPath, "saves");
                if (Directory.Exists(saves)) CopyDir(saves, Path.Combine(tmp, "saves"));
            }

            File.WriteAllText(Path.Combine(tmp, "pack.json"),
                JsonSerializer.Serialize(new
                {
                    name = packName,
                    version = packVersion,
                    gameVersion = "0.98a-RC8",
                    sspcl = true,
                    exportedAt = DateTime.Now.ToString("O"),
                }, new JsonSerializerOptions { WriteIndented = true }));

            if (File.Exists(outZipPath)) File.Delete(outZipPath);
            ZipFile.CreateFromDirectory(tmp, outZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return outZipPath;
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            string rel = f.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string df = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(df)!);
            File.Copy(f, df, overwrite: true);
        }
    }
}
