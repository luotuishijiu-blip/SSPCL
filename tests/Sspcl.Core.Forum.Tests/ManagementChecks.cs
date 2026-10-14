using Sspcl.Core.Launch;
using Sspcl.Core.Mods;
using Sspcl.Core.Store.Forum;
using Sspcl.Core.Versioning;

internal static class ManagementChecks
{
    public static void Run(string root)
    {
        var mod=new ModSpec {Path=root,Folder="myfolder",Id="sample",Name="测试舰船",Authors=new[]{"Author"},VersionRaw="1.0",Version=ModVersion.FromString("1.0")};
        Check(ModManagement.Matches(mod,"sample","舰船",true,"enabled","author",false),"Combined name/status/author search failed.");
        Check(ModManagement.Matches(mod,"sample","MYFOLDER",false,"disabled","",false),"Folder search failed.");
        Check(!ModManagement.Matches(mod,"sample","",false,"enabled","",false) && !ModManagement.Matches(mod,"sample","",true,"all","Someone",false),"Filters ignored.");
        Check(ModManagement.Matches(null,"missing","miss",true,"missing","",false) && !ModManagement.Matches(mod,"sample","",true,"missing","",false),"Missing filter incorrect.");
        Check(ModManagement.Matches(mod,"sample","",true,"updates","",true) && !ModManagement.Matches(mod,"sample","",true,"updates","",false),"Update filter incorrect.");
        var diff=ModManagement.Compare(new[]{"old","same"},new[]{"new","same","dep","dep"},new[]{"dep","removed"});
        Check(diff.Added.SequenceEqual(new[]{"dep","new"}) && diff.Removed.SequenceEqual(new[]{"old"}) && diff.Automatic.SequenceEqual(new[]{"dep"}),"Selection diff lost dependencies or removals.");
        var updates=ModManagement.FindUpdates(new[]{mod},new[]{new ForumMod {Id="sample",Version="unknown",Releases=new(){new(){ModVersion="0.9"},new(){ModVersion="2.1"}}},new ForumMod{Id="sample",Version="99999999999999999999999999999"}});
        Check(updates["sample"]=="2.1","Forum release candidates or malformed version handling failed.");
        Check(ModManagement.FindUpdates(new[]{mod},new[]{new ForumMod{Id="sample",Version="1.0"},new ForumMod{Id="other",Version="9.0"}}).Count==0,"Unrelated/current version marked newer.");

        string game=Path.Combine(root,"diagnostic-game");ModpackChecks.MakeGame(game,"0.98a");
        Directory.CreateDirectory(Path.GetDirectoryName(Launcher.JavaExePath(game))!);File.WriteAllText(Launcher.JavaExePath(game),"fixture, never execute");
        File.WriteAllText(Launcher.VmparamsPath(game),"java.exe -Xmx4g -Xms1g com.fs.starfarer.StarfarerLauncher");
        var report=LaunchDiagnostics.Check(game,Array.Empty<string>());Check(report.CanLaunch,"Valid diagnostic fixture rejected.");
        File.WriteAllText(Launcher.VmparamsPath(game),"java.exe -Xmx1g -Xms4g");
        report=LaunchDiagnostics.Check(game,new[]{"missing"});Check(!report.CanLaunch && report.Items.Any(i=>i.Title=="内存") && report.Items.Any(i=>i.Title=="缺失 MOD / 前置"),"Invalid memory or missing selection not diagnosed.");
        File.WriteAllText(Launcher.VmparamsPath(game),"java.exe -Xmx4g -Xms1g com.fs.starfarer.StarfarerLauncher");
        string mods=Path.Combine(game,"mods");WriteMod(mods,"one","sample","1.0","old-only.txt");WriteMod(mods,"duplicate","sample","1.0","old-only.txt");
        report=LaunchDiagnostics.Check(game,new[]{"sample"});Check(!report.CanLaunch && report.Items.Any(i=>i.Title=="重复 ID"),"Duplicate ID not diagnosed.");
        string requiring=Path.Combine(mods,"requiring");Directory.CreateDirectory(requiring);File.WriteAllText(Path.Combine(requiring,"mod_info.json"),"{\"id\":\"requires\",\"dependencies\":[{\"id\":\"sample\",\"version\":\"2.0\"}],\"jars\":[\"missing.jar\"]}");
        report=LaunchDiagnostics.Check(game,new[]{"requires"});Check(report.Items.Any(i=>i.Title=="前置版本不足") && report.Items.Any(i=>i.Title=="MOD 文件缺失"),"Dependency version and JAR diagnostics missing.");
        Check(!File.Exists(Path.Combine(mods,"enabled_mods.json")),"Read-only diagnostics wrote enabled state.");

        string profilesRoot=Path.Combine(root,"management-profiles");var profiles=new ModProfileStore(profilesRoot);
        var profile=profiles.Save("Original",new[]{"sample"});profiles.Save("Changed",new[]{"requires"},profile.Id);
        var backups=new BackupCatalog(profilesRoot,mods);var profileBackup=backups.List().Single(b=>b.Kind=="profile");
        backups.Restore(profileBackup.Key);Check(profiles.Read(profile.Id).Name=="Original" && profiles.Read(profile.Id).EnabledMods.SequenceEqual(new[]{"sample"}),"Profile backup restore failed.");
        backups.Restore(profileBackup.Key);Check(profiles.Read(profile.Id).Name=="Changed","Restored profile cannot undo restore.");
        string source=Path.Combine(root,"management-source","new-folder");WriteMod(Path.GetDirectoryName(source)!,"new-folder","sample","2.0","new-only.txt");
        var install=ModInstaller.InstallFolder(source,mods);Check(install.Success,"Backup fixture install failed: "+install.Error);
        var oldBackup=backups.List().First(b=>b.Kind=="mod" && b.Version=="1.0");Check(oldBackup.SizeBytes>0 && oldBackup.Folder!="","Backup inventory metadata missing.");
        backups.Restore(oldBackup.Key);var active=ModScanner.Scan(mods).Single(m=>m.Id=="sample");
        Check(active.VersionRaw=="1.0" && File.Exists(Path.Combine(active.Path,"old-only.txt")) && !File.Exists(Path.Combine(active.Path,"new-only.txt")),"MOD restore merged files or lost original folder.");
        var redo=backups.List().First(b=>b.Kind=="mod" && b.Version=="2.0");backups.Restore(redo.Key);
        Check(ModScanner.Scan(mods).Single(m=>m.Id=="sample").VersionRaw=="2.0","MOD restore failed to retain replaced version.");
        backups.Delete(oldBackup.Key);Check(!backups.List().Any(b=>b.Key==oldBackup.Key) && ModScanner.Scan(mods).Single(m=>m.Id=="sample").VersionRaw=="2.0","Deleting backup affected active MOD.");
        backups.Delete(profileBackup.Key);Check(!backups.List().Any(b=>b.Key==profileBackup.Key) && profiles.Read(profile.Id).Name=="Changed","Deleting profile backup affected current profile.");
        foreach(string invalid in new[]{"mod:../../outside","profile:../../outside","mod:"+Guid.NewGuid().ToString("N")})
        {bool rejected=false;try{backups.Delete(invalid);}catch(IOException){rejected=true;}Check(rejected,"Unlisted backup key accepted.");}
        Check(!backups.List().Any(b=>b.Key.Contains(".sspcl-install")),"Stage appeared as backup.");
        Console.WriteLine("Management checks passed: combined filters, missing IDs, update candidates, selection differences, read-only launch diagnostics, whole-folder restore/undo and selective backup deletion.");
    }
    private static void WriteMod(string root,string folder,string id,string version,string file)
    {string dir=Path.Combine(root,folder);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"mod_info.json"),System.Text.Json.JsonSerializer.Serialize(new{id,name=id,version}));File.WriteAllText(Path.Combine(dir,file),version);}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
