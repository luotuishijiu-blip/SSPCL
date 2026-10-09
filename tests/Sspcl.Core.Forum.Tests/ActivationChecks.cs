using Sspcl.Core.Mods;

internal static class ActivationChecks
{
    public static void Run(string root)
    {
        string game = Path.Combine(root, "activation-game"), pool = Path.Combine(root, "activation-pool");
        WriteMod(Path.Combine(game, "mods", "different-folder"), "already", "original");
        WriteMod(Path.Combine(pool, "pool-folder"), "already", "pool");
        WriteMod(Path.Combine(pool, "dependency"), "dependency", "dep");
        string feature = Path.Combine(pool, "feature");
        WriteMod(feature, "feature", "feature");
        File.WriteAllText(Path.Combine(feature, "mod_info.json"), "{\"id\":\"feature\",\"name\":\"Feature\",\"version\":\"1.0\",\"dependencies\":[{\"id\":\"dependency\"}]}");
        var mods = ModScanner.Scan(pool);
        var enabled = ModActivationService.Apply(game, mods, new[] { "already", "feature", "unlisted" });
        Check(enabled.SetEquals(new[] { "already", "feature", "dependency", "unlisted" }), "Dependency or unlisted enabled ID lost.");
        Check(!Directory.Exists(Path.Combine(game, "mods", "pool-folder")), "Same ID in different folder copied twice.");
        Check(File.ReadAllText(Path.Combine(game, "mods", "different-folder", "payload.txt")) == "original", "Existing MOD overwritten.");
        string payload = Path.Combine(game, "mods", "feature", "payload.txt");
        File.WriteAllText(payload, "player-edited");
        ModActivationService.Apply(game, mods, Array.Empty<string>());
        Check(File.Exists(payload) && EnabledModsFile.Read(game).Count == 0, "Disabling deleted files or did not disable.");
        for (int i = 0; i < 5; i++) ModActivationService.Apply(game, mods, new[] { "feature" });
        Check(File.ReadAllText(payload) == "player-edited", "Re-enabling copied over existing files.");
        WriteMod(Path.Combine(game, "mods", "duplicate-folder"), "already", "duplicate");
        ModActivationService.Apply(game, mods, new[] { "already" });
        Check(ModScanner.Scan(Path.Combine(game, "mods")).Count(m => m.Id == "already") == 1, "Duplicate existing ID still present.");
        Check(Directory.GetDirectories(Path.Combine(game, "Sspcl", "ModBackups")).Length == 1, "Duplicate was not preserved as backup.");
        WriteMod(Path.Combine(game, "mods", "blocked-folder"), "other", "other");
        WriteMod(Path.Combine(pool, "blocked-folder"), "blocked", "blocked");
        try { ModActivationService.Apply(game, ModScanner.Scan(pool), new[] { "blocked" }); throw new Exception("Conflicting folder accepted."); }
        catch (IOException) { }
        Check(EnabledModsFile.Read(game).SequenceEqual(new[] { "already" }), "Failed operation changed enabled list.");
        Check(!Directory.GetDirectories(Path.Combine(game, "mods"), ".sspcl-enable-*").Any(), "Copy stage leaked.");
        Console.WriteLine("Activation checks passed: ID reuse, dependencies, disable/re-enable without recopy, duplicate backup, rapid writes and failure preservation.");
    }

    private static void WriteMod(string path, string id, string payload)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "mod_info.json"), "{\"id\":\"" + id + "\",\"name\":\"" + id + "\",\"version\":\"1.0\"}");
        File.WriteAllText(Path.Combine(path, "payload.txt"), payload);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
