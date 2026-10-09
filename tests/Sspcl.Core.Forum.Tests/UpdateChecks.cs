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
            Check(!Directory.GetFiles(modsRoot, "obsolete.txt", SearchOption.AllDirectories).Any(), "Old files merged into new version.");
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
