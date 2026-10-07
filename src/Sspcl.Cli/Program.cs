using System.IO.Compression;
using Sspcl.Core.Install;
using Sspcl.Core.Launch;
using Sspcl.Core.Mods;
using Sspcl.Core.Saves;
using Sspcl.Core.Store;

// 交叉验证 CLI：输出与 scripts/jsonc_dialect.py + probe_saves.py 对应的报告，
// 用于确认 C# 数据层与 Python 对照实现行为一致。
Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
    return SelfTest();

if (args.Contains("--launchcheck", StringComparer.OrdinalIgnoreCase))
{
    var p = args.FirstOrDefault(a => !a.StartsWith("--")) ?? @"D:\Starsector";
    return LaunchCheck(p);
}

if (args.Contains("--installcheck", StringComparer.OrdinalIgnoreCase))
    return InstallCheck();

if (args.Contains("--storecheck", StringComparer.OrdinalIgnoreCase))
    return await StoreCheck();

var dirs = args.Length > 0 ? args : new[] { @"D:\Starsector", @"F:\Starsector" };
int exit = 0;

foreach (var dir in dirs)
{
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine($"Game dir: {dir}");
    if (!Directory.Exists(dir))
    {
        Console.WriteLine("  MISSING");
        exit = 1;
        continue;
    }

    var inst = InstallationDetector.Detect(dir);
    Console.WriteLine($"  Valid: {inst.IsValid}  Version: {inst.Version}  Localization: {inst.LocalizationPackageVersion} (game {inst.LocalizationVersion})  enabled: {inst.EnabledModCount}");
    if (inst.Problems.Count > 0)
        Console.WriteLine("  Problems: " + string.Join("; ", inst.Problems));

    var mods = ModScanner.Scan(Path.Combine(dir, "mods"));
    var ok = mods.Where(m => m.Error is null).ToList();
    var failed = mods.Where(m => m.Error is not null).ToList();
    var report = DependencyResolver.Analyze(mods, inst.ParsedVersion);

    Console.WriteLine($"  mods parsed: {ok.Count}  failed: {failed.Count}");
    foreach (var f in failed)
        Console.WriteLine($"    [FAIL] {f.Folder}: {f.Error}");

    Console.WriteLine($"  duplicates: {report.Duplicates.Count} " +
        string.Join("; ", report.Duplicates.Select(kv => $"{kv.Key}=[{string.Join(", ", kv.Value)}]")));

    Console.WriteLine($"  missing hard deps: {report.Missing.Count}");
    foreach (var m in report.Missing.Take(12))
        Console.WriteLine($"    {m.ModId} -> {m.DepId}");

    Console.WriteLine($"  version mismatch (dep present but older): {report.VersionMismatch.Count}");
    foreach (var vm in report.VersionMismatch.Take(12))
        Console.WriteLine($"    {vm.ModId} needs {vm.DepId} {vm.Required}, installed {vm.Installed}");

    Console.WriteLine($"  over-version (>{inst.Version}): {report.OverVersion.Count}");
    foreach (var o in report.OverVersion.Take(12))
        Console.WriteLine($"    {o.Id} declares {o.GameVersionRaw}");

    Console.WriteLine($"  cycles: {report.Cycles.Count}");

    Console.WriteLine("  version shapes: " + string.Join(", ",
        ok.GroupBy(m => m.VersionShape).Select(g => $"{g.Key}={g.Count()}").OrderBy(s => s, StringComparer.Ordinal)));

    // 存档
    var savesDir = Path.Combine(dir, "saves");
    var allDirs = Directory.Exists(savesDir)
        ? Directory.EnumerateDirectories(savesDir).Select(d => Path.GetFileName(d) ?? "").ToList()
        : new List<string>();
    var saves = SaveScanner.Scan(savesDir);
    var nonSave = allDirs.Where(d => !saves.Any(s => s.Folder == d)).ToList();
    int compressed = saves.Count(s => s.Compressed);
    int noEnabled = saves.Count(s => s.EnabledModIds.Count == 0);

    Console.WriteLine($"  saves: dirs {allDirs.Count} -> real {saves.Count}, non-save {nonSave.Count} [{string.Join(", ", nonSave)}]");
    Console.WriteLine($"  compressed: {compressed}  missing enabledMods: {noEnabled}");
    foreach (var s in saves.Take(6))
        Console.WriteLine($"    {s.Folder} lv{s.CharacterLevel} mods={s.EnabledModIds.Count} {s.SaveDate} {s.CharacterName}");
}

return exit;

static int SelfTest()
{
    var tmp = Path.Combine(Path.GetTempPath(), "sspcl-selftest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(tmp, "mods"));
    try
    {
        EnabledModsFile.Write(tmp, new[] { "lw_lazylib", "MagicLib" });
        EnabledModsFile.Write(tmp, new[] { "lw_lazylib", "MagicLib", "lunalib" });

        var read = EnabledModsFile.Read(tmp);
        bool contentOk = read.SequenceEqual(new[] { "lw_lazylib", "MagicLib", "lunalib" });
        int backups = Directory.GetFiles(Path.Combine(tmp, "mods"), "enabled_mods.json.bak-*").Length;
        string raw = File.ReadAllText(EnabledModsFile.GetPath(tmp));
        bool keyOk = raw.Contains("\"enabledMods\"");

        Console.WriteLine($"selftest: content={contentOk}  backups={backups}  key=enabledMods:{keyOk}");
        return (contentOk && backups >= 1 && keyOk) ? 0 : 1;
    }
    finally
    {
        try { Directory.Delete(tmp, true); } catch { }
    }
}

static int LaunchCheck(string installPath)
{
    string vmparamsPath = Launcher.VmparamsPath(installPath);
    if (!File.Exists(vmparamsPath))
    {
        Console.WriteLine($"vmparams not found: {vmparamsPath}");
        return 1;
    }
    string text = File.ReadAllText(vmparamsPath);
    var (xmx, xms) = Launcher.ReadMemoryMb(text);
    Console.WriteLine($"current memory: -Xmx{xmx}m -Xms{xms}m");

    var args = Launcher.BuildArgs(text, 4096, 3072);
    bool hasXmx = args.Any(a => a == "-Xmx4096m");
    bool hasXms = args.Any(a => a == "-Xms3072m");
    bool hasMain = args.Any(a => a == "com.fs.starfarer.StarfarerLauncher");
    bool javaOk = File.Exists(Launcher.JavaExePath(installPath));
    bool coreOk = Directory.Exists(Launcher.CoreDir(installPath));

    Console.WriteLine($"built args: {args.Count}  hasXmx4096={hasXmx}  hasXms3072={hasXms}  hasMain={hasMain}");
    Console.WriteLine($"java: {Launcher.JavaExePath(installPath)}  exists={javaOk}");
    Console.WriteLine($"core: {Launcher.CoreDir(installPath)}  exists={coreOk}");

    // 跳过启动器的 -D 参数（不真启动，纯构建验证）
    var la = Launcher.BuildLaunchArgs(installPath, 4096, 3072, skipLauncher: true, resolution: "1600x900", fullscreen: false, sound: true);
    int mainIdx = la.FindLastIndex(a => a == "com.fs.starfarer.StarfarerLauncher");
    bool hasLaunchDirect = la.Contains("-DlaunchDirect=true");
    bool hasStartRes = la.Contains("-DstartRes=1600x900");
    bool hasStartFS = la.Contains("-DstartFS=false");
    bool hasStartSound = la.Contains("-DstartSound=true");
    bool flagsBeforeMain = la.IndexOf("-DlaunchDirect=true") < mainIdx;
    Console.WriteLine($"skip-launcher: launchDirect={hasLaunchDirect} startRes={hasStartRes} startFS={hasStartFS} startSound={hasStartSound} flagsBeforeMain={flagsBeforeMain}");

    return (hasXmx && hasXms && hasMain && javaOk && coreOk && hasLaunchDirect && hasStartRes && hasStartFS && hasStartSound && flagsBeforeMain) ? 0 : 1;
}

static int InstallCheck()
{
    var baseDir = Path.Combine(Path.GetTempPath(), "sspcl-installcheck-" + Guid.NewGuid().ToString("N"));
    var src = Path.Combine(baseDir, "src");
    var modRoot = Path.Combine(src, "MyMod-1.0"); // 模拟压缩包内多一层同名目录
    Directory.CreateDirectory(modRoot);
    File.WriteAllText(Path.Combine(modRoot, "mod_info.json"),
        "{\"id\":\"mymod\",\"name\":\"My Mod\",\"version\":\"1.0\",\"gameVersion\":\"0.98a-RC8\"}");
    File.WriteAllText(Path.Combine(modRoot, "data.txt"), "hello");

    var zip = Path.Combine(baseDir, "MyMod.zip");
    ZipFile.CreateFromDirectory(src, zip);

    var modsDir = Path.Combine(baseDir, "mods");
    var result = ModInstaller.InstallArchive(zip, modsDir);

    bool nestedStripped = File.Exists(Path.Combine(modsDir, "MyMod-1.0", "mod_info.json"))
                          && !Directory.Exists(Path.Combine(modsDir, "MyMod-1.0", "MyMod-1.0"));
    bool idOk = result.ModId == "mymod";

    Console.WriteLine($"installcheck: success={result.Success}  nestedStripped={nestedStripped}  id={result.ModId}  folder={result.TargetFolder}");
    try { Directory.Delete(baseDir, true); } catch { }
    return (result.Success && nestedStripped && idOk) ? 0 : 1;
}

static async System.Threading.Tasks.Task<int> StoreCheck()
{
    try
    {
        var items = await ModRepoClient.FetchAsync();
        Console.WriteLine($"store: {items.Count} items");
        foreach (var it in items.Take(5))
            Console.WriteLine($"  {it.Name} | {string.Join(", ", it.Authors)} | dl={it.HasDirectDownload} | {it.HomeUrl}");
        return items.Count > 0 ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine("store fetch failed: " + ex.Message);
        return 1;
    }
}
