using System.Text.Json;

namespace Sspcl.Core.Mods;
public sealed class BackupEntry
{
    public string Key {get;set;}="";
    public string Kind {get;set;}="";
    public string Name {get;set;}="";
    public string Version {get;set;}="";
    public string Folder {get;set;}="";
    public long SizeBytes {get;set;}
    public DateTime CreatedUtc {get;set;}
    public string Display=>Name+" · "+(Kind=="profile"?"方案":"MOD")+" "+Version+" · "+(SizeBytes/1048576d).ToString("F1")+" MB · "+CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
public sealed class BackupCatalog
{
    private readonly ModProfileStore profiles;
    private readonly string mods;
    public BackupCatalog(string profileRoot,string modsRoot){profiles=new(profileRoot);mods=Path.GetFullPath(modsRoot);}
    private string ModRoot=>Path.Combine(mods,".sspcl-backups");
    public List<BackupEntry> List()
    {
        var result=new List<BackupEntry>();
        if(Directory.Exists(profiles.Root))foreach(string dir in Directory.GetDirectories(profiles.Root).Where(d=>Guid.TryParseExact(Path.GetFileName(d),"N",out _) && !Linked(d)))
        {
            string id=Path.GetFileName(dir),file=Path.Combine(dir,"profile.json.bak");
            if(!File.Exists(file) || Linked(file))continue;
            try {var saved=ReadProfile(id);result.Add(new(){Key="profile:"+id,Kind="profile",Name=saved.Name,SizeBytes=new FileInfo(file).Length,CreatedUtc=File.GetLastWriteTimeUtc(file)});}
            catch(Exception ex) when(ex is IOException || ex is JsonException || ex is UnauthorizedAccessException) {continue;}
        }
        if(Directory.Exists(ModRoot) && !Linked(ModRoot))foreach(string dir in Directory.GetDirectories(ModRoot).Where(d=>Guid.TryParseExact(Path.GetFileName(d),"N",out _) && !Linked(d)))
        {
            try
            {
                string info=Path.Combine(dir,"mod_info.json");if(!File.Exists(info))continue;
                var spec=ModScanner.ParseModInfo(File.ReadAllText(info),dir,Path.GetFileName(dir));if(string.IsNullOrWhiteSpace(spec.Id) || !string.IsNullOrWhiteSpace(spec.Error))continue;
                string key=Path.GetFileName(dir),folder=spec.Id;
                string metadata=Path.Combine(ModRoot,"index",key+".json");
                if(Directory.Exists(Path.GetDirectoryName(metadata)!) && Linked(Path.GetDirectoryName(metadata)!))continue;
                if(File.Exists(metadata) && !Linked(metadata)){using var json=JsonDocument.Parse(File.ReadAllText(metadata));folder=json.RootElement.GetProperty("folder").GetString()??spec.Id;}
                ValidateFolder(folder);
                result.Add(new(){Key="mod:"+key,Kind="mod",Name=spec.Name,Version=spec.VersionRaw,Folder=folder,SizeBytes=Files(dir).Sum(f=>new FileInfo(f).Length),CreatedUtc=Directory.GetLastWriteTimeUtc(dir)});
            }
            catch(Exception ex) when(ex is IOException || ex is JsonException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is OverflowException || ex is FormatException) {continue;}
        }
        return result.OrderByDescending(e=>e.CreatedUtc).ToList();
    }
    public static void RememberFolder(string backup,string folder)
    {
        ValidateFolder(folder);string index=Path.Combine(Path.GetDirectoryName(backup)!,"index");Directory.CreateDirectory(index);
        if(Linked(index))throw new IOException("备份索引不能使用目录链接。");
        ModProfileStore.AtomicWrite(Path.Combine(index,Path.GetFileName(backup)+".json"),JsonSerializer.Serialize(new{folder}));
    }
    internal static void PrepareRoot(string modsRoot)
    {
        string root=Path.Combine(modsRoot,".sspcl-backups");Directory.CreateDirectory(root);
        if(Linked(root))throw new IOException("备份目录不能使用目录链接。");
    }
    private BackupEntry Find(string key)=>List().SingleOrDefault(e=>e.Key==key)??throw new IOException("备份已不存在或内容无效，请刷新列表。");
    public void Restore(string key)
    {
        var entry=Find(key);string id=key.Split(':')[1];
        if(entry.Kind=="profile") {var saved=ReadProfile(id);profiles.Save(saved.Name,saved.EnabledMods,id);return;}
        string source=Path.Combine(ModRoot,id);_ = Files(source).Count();
        var result=ModInstaller.InstallFolder(source,mods,entry.Folder);
        if(!result.Success)throw new IOException("恢复 MOD 失败："+result.Error);
    }
    public void Delete(string key)
    {
        var entry=Find(key);string id=key.Split(':')[1];
        if(entry.Kind=="profile")File.Delete(Path.Combine(profiles.DirectoryOf(id),"profile.json.bak"));
        else {string dir=Path.Combine(ModRoot,id);_ = Files(dir).Count();Directory.Delete(dir,true);File.Delete(Path.Combine(ModRoot,"index",id+".json"));}
    }
    private ModProfile ReadProfile(string id)
    {
        var value=JsonSerializer.Deserialize<ModProfile>(File.ReadAllText(Path.Combine(profiles.DirectoryOf(id),"profile.json.bak")))??throw new InvalidDataException("备份为空。");
        if(value.Id!=id || string.IsNullOrWhiteSpace(value.Name) || value.EnabledMods==null || value.EnabledMods.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("方案备份无效。");return value;
    }
    private static bool Linked(string path)=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;
    private static void ValidateFolder(string folder)
    {if(string.IsNullOrWhiteSpace(folder) || folder=="." || folder==".." || folder.StartsWith(".sspcl-",StringComparison.OrdinalIgnoreCase) || Path.GetFileName(folder)!=folder || folder.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new IOException("备份记录的目录名字无效。");}
    private static IEnumerable<string> Files(string dir)
    {
        if(Linked(dir))throw new IOException("不处理包含目录链接的备份。");
        foreach(string file in Directory.GetFiles(dir)){if(Linked(file))throw new IOException("不处理包含文件链接的备份。");yield return file;}
        foreach(string sub in Directory.GetDirectories(dir))foreach(string file in Files(sub))yield return file;
    }
}
