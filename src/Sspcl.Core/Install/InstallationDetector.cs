using System.IO.Compression;
using System.Text.Json;
using Sspcl.Core.Mods;
using Sspcl.Core.Saves;

namespace Sspcl.Core.Install;

/// <summary>检测一份远行星号安装目录：合法性、版本、汉化版本、mod/存档计数。</summary>
public static class InstallationDetector
{
    public static Installation Detect(string path)
    {
        var problems = new List<string>();
        var core = Path.Combine(path, "starsector-core");
        var exe = Path.Combine(path, "starsector.exe");
        var jar = Path.Combine(core, "starfarer_obf.jar");
        var mods = Path.Combine(path, "mods");
        var vmparams = Path.Combine(path, "vmparams");

        bool valid = true;
        if (!File.Exists(exe)) problems.Add("缺少 starsector.exe");
        if (!File.Exists(jar)) { problems.Add("缺少 starsector-core\\starfarer_obf.jar"); valid = false; }
        if (!Directory.Exists(mods)) problems.Add("缺少 mods 目录");
        if (!File.Exists(vmparams)) problems.Add("缺少 vmparams");

        string version = File.Exists(jar) ? ReadCoreVersion(jar) : "";
        string locPackage = "", locVersion = "";
        var locPath = Path.Combine(core, "localization_version.json");
        if (File.Exists(locPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(locPath));
                if (doc.RootElement.TryGetProperty("game_version", out var gv)) locVersion = gv.GetString() ?? "";
                if (doc.RootElement.TryGetProperty("version", out var v)) locPackage = v.GetString() ?? "";
            }
            catch { /* 汉化信息读取失败不致命 */ }
        }

        int modCount = ModScanner.Scan(mods).Count;
        int enabledCount = EnabledModsFile.Read(path).Count;
        int saveCount = SaveScanner.Scan(Path.Combine(path, "saves")).Count;

        string folder = Path.GetFileName(path.TrimEnd('\\', '/'));
        string drive = Path.GetPathRoot(path)?.TrimEnd('\\', ':') ?? "";

        return new Installation
        {
            Path = path,
            DisplayName = folder + (drive.Length > 0 ? $" ({drive})" : ""),
            IsValid = valid,
            Problems = problems,
            Version = version,
            LocalizationPackageVersion = locPackage,
            LocalizationVersion = locVersion,
            ModCount = modCount,
            EnabledModCount = enabledCount,
            SaveCount = saveCount,
        };
    }

    private static string ReadCoreVersion(string jarPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(jarPath);
            var entry = zip.GetEntry("com/fs/starfarer/Version.class");
            if (entry == null) return "";
            using var ms = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(ms);
            return ClassFileConstantPool.FindVersion(ClassFileConstantPool.ReadUtf8Strings(ms.ToArray())) ?? "";
        }
        catch
        {
            return "";
        }
    }
}
