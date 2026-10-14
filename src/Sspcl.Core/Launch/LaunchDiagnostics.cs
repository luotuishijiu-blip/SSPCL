using Sspcl.Core.Mods;
using Sspcl.Core.Versioning;

namespace Sspcl.Core.Launch;
public sealed class DiagnosticItem
{
    public string Level {get;set;}="信息";
    public string Title {get;set;}="";
    public string Detail {get;set;}="";
}
public sealed class LaunchDiagnosticReport
{
    public List<DiagnosticItem> Items {get;}=new();
    public bool CanLaunch=>Items.All(i=>i.Level!="错误");
    public string ToText()=>"SSPCL 启动诊断\n"+string.Join("\n",Items.Select(i=>"["+i.Level+"] "+i.Title+"："+i.Detail));
}
public static class LaunchDiagnostics
{
    public static LaunchDiagnosticReport Check(string game,IEnumerable<string> requested)
    {
        var report=new LaunchDiagnosticReport();
        void Add(string level,string title,string detail)=>report.Items.Add(new(){Level=level,Title=title,Detail=detail});
        if(!File.Exists(Path.Combine(game,"starsector-core","starfarer_obf.jar")))Add("错误","游戏目录","缺少核心 JAR，请重新选择完整游戏目录。");else Add("信息","游戏目录",game);
        if(!File.Exists(Launcher.JavaExePath(game)))Add("错误","Java","缺少 jre/bin/javaw.exe，请修复游戏运行环境。");else Add("信息","Java",Launcher.JavaExePath(game));
        try
        {
            string text=File.ReadAllText(Launcher.VmparamsPath(game));var memory=Launcher.ReadMemoryMb(text);
            if(memory.XmxMb<=0 || memory.XmsMb<=0 || memory.XmsMb>memory.XmxMb)Add("错误","内存","Xmx/Xms 必须大于零，且 Xms 不能超过 Xmx。");
            else Add("信息","内存","Xmx "+memory.XmxMb+" MB / Xms "+memory.XmsMb+" MB");
            if(!Launcher.Tokenize(text).Contains("com.fs.starfarer.StarfarerLauncher"))Add("错误","启动参数","vmparams 缺少游戏启动主类。");
        }
        catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is OverflowException) {Add("错误","启动参数","无法读取有效 vmparams："+ex.Message);}
        try
        {
            var mods=ModScanner.Scan(Path.Combine(game,"mods"));var selected=ModProfileStore.ResolveSelection(requested,mods);
            var missing=ModProfileStore.Missing(selected,mods);
            if(missing.Count>0)Add("错误","缺失 MOD / 前置",string.Join("、",missing));
            var active=mods.Where(m=>selected.Contains(m.Id)).ToList();var dependencies=DependencyResolver.Analyze(active,GameVersion.Unknown);
            if(dependencies.Duplicates.Count>0)Add("错误","重复 ID",string.Join("、",dependencies.Duplicates.Keys));
            foreach(var problem in dependencies.VersionMismatch)Add("错误","前置版本不足",problem.ModId+" 需要 "+problem.DepId+" "+problem.Required+"，已安装 "+problem.Installed);
            foreach(var cycle in dependencies.Cycles)Add("警告","依赖循环",string.Join(" → ",cycle));
            foreach(var mod in active)
                foreach(string jar in mod.Jars)
                {
                    string path=Path.GetFullPath(Path.Combine(mod.Path,jar));
                    if(!path.StartsWith(Path.GetFullPath(mod.Path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !File.Exists(path))Add("错误","MOD 文件缺失",mod.Id+" 缺少有效 JAR："+jar);
                }
            Add("信息","启用清单",selected.Count+" 个 ID，检查含自动补齐的前置。");
        }
        catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {Add("错误","MOD 目录",ex.Message);}
        return report;
    }
}
