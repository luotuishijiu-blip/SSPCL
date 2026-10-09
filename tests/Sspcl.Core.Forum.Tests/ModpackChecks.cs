using System.IO.Compression;
using System.Net;
using System.Text;
using Sspcl.Core.Downloads;
using Sspcl.Core.Install;
using Sspcl.Core.Modpack;
using Sspcl.Core.Mods;
using Sspcl.Core.Store.Forum;

internal static class ModpackChecks
{
    public static async Task Run(string root)
    {
        string source = Path.Combine(root, "base-game");
        string pool = Path.Combine(root, "local-pool");
        MakeGame(source, "0.98a-RC8");
        Write(Path.Combine(source, "starsector-core/data/config/settings.json"), "{\"preset\":true}");
        Write(Path.Combine(source, "mods/local/mod_info.json"), "{\"id\":\"local\",\"name\":\"Local MOD\",\"version\":\"1.0\"}");
        Write(Path.Combine(source, "mods/local/plugin.jar"), "MOD-BINARY-MUST-NOT-BE-EXPORTED");
        Write(Path.Combine(source, "saves/player/descriptor.xml"), "<SaveGameData><characterName>Fixture</characterName></SaveGameData>");
        Write(Path.Combine(source, "saves/player/campaign.xml"), "<campaign>fixture</campaign>");
        Write(Path.Combine(source, "saves/common/config/preset.json"), "{\"value\":5}");
        Write(Path.Combine(source, "saves/common/config/plugin.dll"), "must-not-export");
        Write(Path.Combine(pool, "pooled/mod_info.json"), "{\"id\":\"pooled\",\"name\":\"Pooled\",\"version\":\"2.0\"}");
        EnabledModsFile.Write(source, new[] { "local", "pooled", "missing" });
        string archive = Path.Combine(root, "shared.sspack");
        ModpackExporter.Export(source, archive, "Shared preset", new ExportOptions { IncludeSaves = true });
        var manifest = ModpackImporter.Inspect(archive);
        Check(manifest.GameVersion == "0.98a-RC8" && manifest.Mods!.Count == 3 && manifest.Name == "Shared preset", "Manifest metadata missing.");
        using (var zip = ZipFile.OpenRead(archive))
        {
            Check(zip.Entries.All(entry => !entry.FullName.StartsWith("mods/") && !entry.FullName.EndsWith(".jar") && !entry.FullName.EndsWith(".dll")), "MOD binary exported.");
            Check(zip.GetEntry("settings/settings.json") != null && zip.GetEntry("saves/player/campaign.xml") != null, "Optional content missing.");
        }
        string imported = Path.Combine(root, "imported");
        var result = ModpackImporter.Import(archive, source, imported, pool, new ExportOptions { IncludeSaves = true });
        Check(result.MissingMods.Count == 1 && result.MissingMods[0].Id == "missing", "Missing MOD analysis incorrect.");
        Check(EnabledModsFile.Read(imported).SequenceEqual(new[] { "local", "pooled" }), "Local MOD matches not applied.");
        Check(File.Exists(Path.Combine(imported, "mods/local/plugin.jar")) && File.Exists(Path.Combine(imported, "saves/player/campaign.xml")), "Instance was not composed from local files and save data.");
        Check(InstallationDetector.Detect(imported).IsValid && ModpackImporter.ReadInstalledManifest(imported)!.Name == "Shared preset", "Imported version not recognized.");
        Check(EnabledModsFile.Read(source).Count == 3, "Base game was modified.");
        string optional = Path.Combine(root, "optional.sspack");
        var none = new ExportOptions { IncludeModList = false, IncludeGameSettings = false, IncludeGameVersion = false, IncludePackName = false, IncludeSaves = false };
        ModpackExporter.Export(source, optional, "Hidden", none);
        using (var zip = ZipFile.OpenRead(optional)) Check(zip.Entries.Count == 1, "Unselected content exported.");
        var minimal = ModpackImporter.Import(archive, source, Path.Combine(root, "minimal"), pool, none);
        Check(minimal.Manifest.Name == null && minimal.Manifest.Mods == null && EnabledModsFile.Read(minimal.Path).Count == 0 && !File.Exists(Path.Combine(minimal.Path, "saves/player/campaign.xml")), "Unselected imported content applied.");
        string wrong = Path.Combine(root, "wrong-game");
        MakeGame(wrong, "0.97a-RC11");
        Expect<InvalidOperationException>(() => ModpackImporter.Import(archive, wrong, Path.Combine(root, "wrong-import"), pool, new ExportOptions()));
        Expect<IOException>(() => ModpackImporter.Import(archive, source, imported, pool, none));
        Expect<IOException>(() => ModpackImporter.Import(archive, source, Path.Combine(source, "nested"), pool, none));
        foreach (var entry in new[] { "mods/bad/mod_info.json", "mods/bad/plugin.jar", "../escape.xml", "saves/player/plugin.dll", "starsector-core/game.jar" })
        {
            string invalid = Path.Combine(root, "invalid.sspack");
            using (var zip = ZipFile.Open(invalid, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry(entry).Open())) writer.Write("fixture");
            Expect<InvalidDataException>(() => ModpackImporter.Inspect(invalid));
            File.Delete(invalid);
        }
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            string canceled = Path.Combine(root, "canceled-instance");
            Expect<OperationCanceledException>(() => ModpackImporter.Import(archive, source, canceled, pool, none, cancellation.Token));
            Check(!Directory.Exists(canceled) && !Directory.GetDirectories(root, ".sspcl-import-*").Any(), "Canceled import left a partial instance.");
        }
        using var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[] { 0x4D, 0x5A, 0x50, 0, 2, 0, 0, 0 }) }));
        var transfer = new HttpPackageDownloader(http, Path.Combine(root, "installer-cache"));
        using (var downloaded = await transfer.DownloadAsync(new Uri("https://files.test/game.attach"), allowExecutable: true))
            Check(Path.GetExtension(downloaded.Path) == ".exe", "Game installer format not detected.");
        try { await transfer.DownloadAsync(new Uri("https://files.test/mod.attach")); throw new Exception("EXE accepted as MOD archive."); }
        catch (InvalidDataException) { }
        Console.WriteLine("Modpack checks passed: metadata, options, no MOD export, local composition, missing MODs, version validation, no overwrite, forbidden paths, cancellation and EXE download isolation.");
    }
    internal static void MakeGame(string root, string version)
    {
        Directory.CreateDirectory(Path.Combine(root, "starsector-core"));
        Write(Path.Combine(root, "starsector.exe"), "fixture");
        Write(Path.Combine(root, "vmparams"), "-Xmx4096m");
        using var zip = ZipFile.Open(Path.Combine(root, "starsector-core/starfarer_obf.jar"), ZipArchiveMode.Create);
        using var stream = zip.CreateEntry("com/fs/starfarer/Version.class").Open();
        var bytes = Encoding.UTF8.GetBytes(version);
        stream.Write(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE, 0, 0, 0, 52, 0, 2, 1, 0, (byte)bytes.Length });
        stream.Write(bytes);
    }
    private static void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Expect<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
