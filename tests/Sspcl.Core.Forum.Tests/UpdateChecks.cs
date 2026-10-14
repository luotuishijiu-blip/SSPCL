using System.IO.Compression;
using System.Text.Json;
using Sspcl.Core.Mods;

internal static class UpdateChecks
{
    public static void Run(string root)
    {
        string game = Path.Combine(root, "update-game"), pool = Path.Combine(root, "update-pool");
        ModpackChecks.MakeGame(game, "0.98a-RC8");
        foreach (string old in new[] { Path.Combine(game, "mods", "old-folder"), Path.Combine(pool, "old-pool"), Path.Combine(game, "mods", "old-duplicate") })
        {
            Directory.CreateDirectory(old);
            File.WriteAllText(Path.Combine(old, "mod_info.json"), "{\"id\":\"update_fixture\",\"name\":\"Old\",\"version\":\"1.0\",\"gameVersion\":\"0.98a\"}");
            File.WriteAllText(Path.Combine(old, "obsolete.txt"), "stale data");
        }
        EnabledModsFile.Write(game, new[] { "update_fixture", "other_enabled" });
        string archive = Path.Combine(root, "update.zip");
        MakeArchive(archive, "update_fixture", "0.98a", true);
        var result = ModUpdateService.UpdateArchive(archive, "update_fixture", pool, game);
        Check(result.Success && result.Error.Length == 0, "Compatible update failed: " + result.Error);
        Check(EnabledModsFile.Read(game).SequenceEqual(new[] { "update_fixture", "other_enabled" }), "Update changed enabled state.");
        foreach (string modsRoot in new[] { pool, Path.Combine(game, "mods") })
        {
            var installed = ModScanner.Scan(modsRoot);
            Check(installed.Count(m => m.Id == "update_fixture") == 1 && installed.Single(m => m.Id == "update_fixture").VersionRaw == "2.0", "Old or duplicate version remains.");
            Check(!installed.Any(m => File.Exists(Path.Combine(m.Path, "obsolete.txt"))), "Old files merged into active new version.");
            Check(new BackupCatalog(Path.Combine(root, "backup-profiles"), modsRoot).List().Any(e => e.Kind == "mod" && e.Version == "1.0"), "Update did not retain recoverable old MOD.");
            Check(File.Exists(Path.Combine(modsRoot, "new-folder", "new.jar")), "New files not installed.");
        }
        Check(!Directory.GetDirectories(game, ".sspcl-old-*").Any() && !Directory.GetDirectories(root, ".sspcl-old-*").Any(), "Old backups not deleted after success.");
        foreach (var invalid in new[] { (Id: "wrong_id", Game: "0.98a", Jar: true), (Id: "update_fixture", Game: "0.97a", Jar: true), (Id: "update_fixture", Game: "0.98a", Jar: false) })
        {
            MakeArchive(archive, invalid.Id, invalid.Game, invalid.Jar);
            result = ModUpdateService.UpdateArchive(archive, "update_fixture", pool, game);
            Check(!result.Success, "Wrong ID/game version or incomplete update accepted.");
            Check(File.Exists(Path.Combine(game, "mods", "new-folder", "new.jar")) && ModScanner.Scan(pool).Single().VersionRaw == "2.0", "Rejected update changed old files.");
        }
        Check(ModUpdateService.IsCompatible("0.98", "0.98a-RC8") && ModUpdateService.IsCompatible("0.98a", "0.98a-RC8"), "Broad forum game versions rejected.");
        Check(!ModUpdateService.IsCompatible("0.97", "0.98a-RC8") && !ModUpdateService.IsCompatible("0.98a-RC7", "0.98a-RC8") && !ModUpdateService.IsCompatible("", "0.98a-RC8"), "Incompatible/unknown game versions accepted.");
        Check(File.Exists(archive) && !Directory.GetDirectories(pool, ".sspcl-new-*").Any(), "Source archive removed or staging leaked.");
        string downloadRoot = Path.Combine(root,"store-install"), sourceFolder = Path.Combine(root,"source","new-folder");Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(Path.Combine(downloadRoot,"old"));File.WriteAllText(Path.Combine(downloadRoot,"old/mod_info.json"),"{\"id\":\"store-id\",\"version\":\"1.0\"}");File.WriteAllText(Path.Combine(downloadRoot,"old/old.txt"),"keep");
        File.WriteAllText(Path.Combine(sourceFolder,"mod_info.json"),"{\"id\":\"store-id\",\"version\":\"2.0\",\"jars\":[\"plugin.jar\"]}");
        var installation=ModInstaller.InstallFolder(sourceFolder,downloadRoot);Check(!installation.Success && File.Exists(Path.Combine(downloadRoot,"old/old.txt")),"Incomplete store update removed old MOD.");
        File.WriteAllText(Path.Combine(sourceFolder,"plugin.jar"),"new");Directory.CreateDirectory(Path.Combine(downloadRoot,"new-folder"));File.WriteAllText(Path.Combine(downloadRoot,"new-folder/mod_info.json"),"{\"id\":\"unrelated\"}");
        installation=ModInstaller.InstallFolder(sourceFolder,downloadRoot);Check(!installation.Success && File.Exists(Path.Combine(downloadRoot,"old/old.txt")) && ModScanner.Scan(downloadRoot).Any(m=>m.Id=="unrelated"),"Same-name store import removed another MOD.");
        Directory.Move(Path.Combine(downloadRoot,"new-folder"),Path.Combine(downloadRoot,"unrelated"));
        installation=ModInstaller.InstallFolder(sourceFolder,downloadRoot);Check(installation.Success && installation.WasUpdate && File.Exists(Path.Combine(downloadRoot,"new-folder/plugin.jar")) && !Directory.Exists(Path.Combine(downloadRoot,"old")),"Transactional store replacement failed.");
        string rollbackRoot=Path.Combine(root,"install-rollback");
        foreach(string folder in new[]{"a-old","z-locked"}) { Directory.CreateDirectory(Path.Combine(rollbackRoot,folder));File.WriteAllText(Path.Combine(rollbackRoot,folder,"mod_info.json"),"{\"id\":\"rollback\"}");File.WriteAllText(Path.Combine(rollbackRoot,folder,"data.txt"),"old"); }
        File.WriteAllText(Path.Combine(sourceFolder,"mod_info.json"),"{\"id\":\"rollback\"}");
        using(var locked=new FileStream(Path.Combine(rollbackRoot,"z-locked/data.txt"),FileMode.Open,FileAccess.Read,FileShare.None))
        { installation=ModInstaller.InstallFolder(sourceFolder,rollbackRoot);Check(!installation.Success && File.Exists(Path.Combine(rollbackRoot,"a-old/data.txt")),"Failed second rename did not restore first old MOD."); }
        string hidden=Path.Combine(downloadRoot,".sspcl-install-fixture");Directory.CreateDirectory(hidden);File.WriteAllText(Path.Combine(hidden,"mod_info.json"),"{\"id\":\"hidden\"}");Check(!ModScanner.Scan(downloadRoot).Any(m=>m.Id=="hidden"),"Private install stage leaked into MOD catalog.");
        Console.WriteLine("Update checks passed: complete replacement in pool/game, obsolete files removed, duplicate cleanup, enabled state preservation, incompatible/invalid package rejection.");
    }

    private static void MakeArchive(string path, string id, string gameVersion, bool includeJar)
    {
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(zip.CreateEntry("new-folder/mod_info.json").Open()))
            writer.Write(JsonSerializer.Serialize(new { id, name = "Updated", version = "2.0", gameVersion, jars = new[] { "new.jar" } }));
        if (includeJar) using (var writer = new StreamWriter(zip.CreateEntry("new-folder/new.jar").Open())) writer.Write("new-only");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
