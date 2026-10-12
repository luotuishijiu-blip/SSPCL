using System.IO.Compression;
using System.Text.Json;
using Sspcl.Core.Mods;
using Sspcl.Core.Modpack;

internal static class ProfileChecks
{
    public static void Run(string root)
    {
        string workspace = Path.Combine(root,"preferences","workspace.json"), junctionGame = Path.Combine(root,"junction-game"), folderA = Path.Combine(root,"collection-a"), folderB = Path.Combine(root,"collection-b");
        Directory.CreateDirectory(Path.Combine(junctionGame,"mods"));Directory.CreateDirectory(folderA);Directory.CreateDirectory(folderB);
        File.WriteAllText(Path.Combine(junctionGame,"mods","original.txt"),"keep");File.WriteAllText(Path.Combine(folderA,"a.txt"),"a");File.WriteAllText(Path.Combine(folderB,"b.txt"),"b");
        SharedWorkspace.Save(workspace,new WorkspacePreferences{GamePath=junctionGame,ModsPath=folderA,ProfileId="saved"});
        var reread=SharedWorkspace.Read(workspace);Check(reread.GamePath==junctionGame && reread.ModsPath==folderA && reread.ProfileId=="saved","Paths lost on reload.");
        SharedWorkspace.Save(workspace,reread);File.WriteAllText(workspace,"{broken");Check(SharedWorkspace.Read(workspace).GamePath==junctionGame,"Corrupt preferences did not recover backup.");
        SharedWorkspace.ConnectMods(junctionGame,folderA);Check(File.Exists(Path.Combine(junctionGame,"mods/a.txt")),"Fixed mods entry does not use selected directory.");
        SharedWorkspace.ConnectMods(junctionGame,folderB);Check(File.Exists(Path.Combine(junctionGame,"mods/b.txt")) && File.Exists(Path.Combine(folderA,"a.txt")),"Switch renamed or removed collection.");
        Check(SharedWorkspace.IsConnected(junctionGame,folderB) && SharedWorkspace.ConnectMods(junctionGame,Path.Combine(junctionGame,"mods"))==folderB,"Selecting fixed mods entry lost physical target.");
        SharedWorkspace.ConnectMods(junctionGame,folderB);Check(Directory.GetDirectories(junctionGame,"mods-backup-*").Length==1,"Reopen repeatedly backed up mods.");
        Expect<IOException>(()=>SharedWorkspace.ConnectMods(junctionGame,junctionGame));
        // Remove only our temporary junction so recursive test cleanup cannot follow it.
        Directory.Delete(Path.Combine(junctionGame,"mods"));
        string game = Path.Combine(root, "shared-game"), old = Path.Combine(root, "old-game"), pool = Path.Combine(root, "legacy-pool");
        WriteMod(Path.Combine(game,"mods","base"),"base","current");
        WriteMod(Path.Combine(pool,"base-old"),"base","old");
        WriteMod(Path.Combine(old,"mods","feature"),"feature","feature", "base");
        EnabledModsFile.Write(old,new[]{"feature"});
        var store = new ModProfileStore(Path.Combine(root,"profiles"));
        store.MigrateLists(new[]{old}); store.MigrateLists(new[]{old});
        Check(store.List().Count==1,"Repeated migration duplicated profiles.");
        ModProfileStore.MergeLegacyMods(game,new[]{pool,Path.Combine(old,"mods")});
        ModProfileStore.MergeLegacyMods(game,new[]{pool,Path.Combine(old,"mods")});
        Check(ModScanner.Scan(Path.Combine(game,"mods")).Count==2 && File.ReadAllText(Path.Combine(game,"mods/base/payload.txt"))=="current","Migration replaced current files or duplicated MODs.");
        Check(File.Exists(Path.Combine(pool,"base-old/payload.txt")) && File.Exists(Path.Combine(old,"mods/feature/payload.txt")),"Migration deleted original files.");
        var first = store.Save("方案 A",new[]{"feature"}); var second=store.Save("方案 A",new[]{"base"});
        Check(first.Id!=second.Id,"New profile replaced same-named profile.");
        store.Apply(first.Id,game);Check(EnabledModsFile.Read(game).ToHashSet().SetEquals(new[]{"feature","base"}),"Dependency not enabled.");
        var payloads=Directory.GetFiles(Path.Combine(game,"mods"),"payload.txt",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllText);
        store.Apply(second.Id,game);Check(EnabledModsFile.Read(game).SequenceEqual(new[]{"base"}),"Switch did not use exact saved IDs.");
        store.Save("方案 B",Array.Empty<string>(),second.Id);store.Apply(second.Id,game);Check(EnabledModsFile.Read(game).Count==0,"Replace/empty profile failed.");
        Check(payloads.All(p=>File.ReadAllText(p.Key)==p.Value),"Switch changed MOD files.");
        var missing=store.Save("缺失",new[]{"unknown"});Expect<InvalidOperationException>(()=>store.Apply(missing.Id,game));Check(EnabledModsFile.Read(game).Count==0,"Failed launch changed list.");
        Expect<ArgumentException>(()=>store.Save(" ",new[]{"base"}));Expect<ArgumentException>(()=>store.DirectoryOf("../escape"));
        string archive=Path.Combine(root,"profile.sspack");
        using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))
        {
            Entry(zip,"pack.json",JsonSerializer.Serialize(new PackManifest {Name="整合方案",GameVersion="0.97a-RC11",HasSettings=true,HasSaves=true,Mods=new(){new(){Id="base",Version="different"}}}));
            Entry(zip,"settings/settings.json","{\"shared\":true}");
            Entry(zip,"saves/imported/descriptor.xml","<save/>");Entry(zip,"saves/imported/campaign.xml","progress");
        }
        var imported=ProfilePackImporter.Import(archive,store,new ExportOptions{IncludeSaves=true});
        Check(!Directory.Exists(Path.Combine(store.DirectoryOf(imported.Id),"mods")) && !Directory.Exists(Path.Combine(store.DirectoryOf(imported.Id),"starsector-core")),"Pack duplicated game/MOD files.");
        ProfilePackImporter.Apply(store,imported.Id,game);
        Check(File.ReadAllText(Path.Combine(game,"saves/imported/campaign.xml"))=="progress","Multi-file save only partially imported.");
        File.WriteAllText(Path.Combine(game,"saves/imported/campaign.xml"),"new progress");ProfilePackImporter.Apply(store,imported.Id,game);
        Check(File.ReadAllText(Path.Combine(game,"saves/imported/campaign.xml"))=="new progress","Launching reset player progress.");
        var invalid=store.Save("失败回滚",new[]{"feature"});
        Directory.CreateDirectory(Path.Combine(store.DirectoryOf(invalid.Id),"payload/settings"));File.WriteAllText(Path.Combine(store.DirectoryOf(invalid.Id),"payload/settings/settings.json"),"changed");
        WriteMod(Path.Combine(game,"mods","duplicate"),"base","duplicate");
        Expect<InvalidOperationException>(()=>ProfilePackImporter.Apply(store,invalid.Id,game));
        Check(File.ReadAllText(Path.Combine(game,"starsector-core/data/config/settings.json"))=="{\"shared\":true}","Failed launch did not restore settings.");
        using(var cancellation=new CancellationTokenSource()) { cancellation.Cancel();Expect<OperationCanceledException>(()=>ProfilePackImporter.Import(archive,store,new ExportOptions(),cancellation.Token)); }
        Check(!Directory.GetDirectories(store.Root,".import-*").Any(),"Import stage leaked.");
        string update=Path.Combine(root,"shared-update.zip");
        using(var zip=ZipFile.Open(update,ZipArchiveMode.Create)) { Entry(zip,"new/mod_info.json","{\"id\":\"base\",\"name\":\"base\",\"version\":\"2.0\",\"gameVersion\":\"0.12a\"}");Entry(zip,"new/new.txt","new"); }
        var updated=ModUpdateService.UpdateArchive(update,"base",Path.Combine(game,"mods"),game,false);
        Check(updated.Success && ModScanner.Scan(Path.Combine(game,"mods")).Count(m=>m.Id=="base")==1 && !File.Exists(Path.Combine(game,"mods/base/payload.txt")),"Shared update retained obsolete directories or enforced game-version isolation.");
        Console.WriteLine("Profile checks passed: migration, shared files, new/replace, exact selection, dependencies, missing IDs, profile packs, multi-file saves, progress preservation and settings rollback.");
    }
    private static void WriteMod(string path,string id,string payload,string? dependency=null)
    { Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"mod_info.json"),JsonSerializer.Serialize(new {id,name=id,version="1.0",dependencies=dependency==null?Array.Empty<object>():new object[]{new{id=dependency}}}));File.WriteAllText(Path.Combine(path,"payload.txt"),payload); }
    private static void Entry(ZipArchive zip,string name,string text) { using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(text); }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static void Expect<T>(Action action) where T:Exception { try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name); }
}
