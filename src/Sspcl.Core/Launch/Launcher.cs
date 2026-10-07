using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Sspcl.Core.Utility;

namespace Sspcl.Core.Launch;

/// <summary>
/// 解析安装根目录的 vmparams 并启动游戏。
/// vmparams 首 token 是 "java.exe"，其后是完整 JVM 参数（含 -Xmx/-Xms 与
/// -classpath ... com.fs.starfarer.StarfarerLauncher）。工作目录须为 starsector-core。
///
/// 「接管游戏自带启动器」：写入 saves\.launching 标志 + 追加
/// -DlaunchDirect / -DstartRes / -DstartFS / -DstartSound，直接进入游戏主菜单。
/// （机制来自本体字节码：StarfarerSettings.??0000() 读 System.getProperty("launchDirect")，
///   ooOO.?00000() 检查 saves\.launching 文件是否存在。）
/// </summary>
public static partial class Launcher
{
    public static string JavaExePath(string installPath) => Path.Combine(installPath, "jre", "bin", "javaw.exe");
    public static string CoreDir(string installPath) => Path.Combine(installPath, "starsector-core");
    public static string VmparamsPath(string installPath) => Path.Combine(installPath, "vmparams");
    public static string SavesDir(string installPath) => Path.Combine(installPath, "saves");

    /// <summary>创建 saves\.launching 标志，告知游戏本次为「直接启动」。</summary>
    public static void SetLaunchFlag(string installPath)
    {
        Directory.CreateDirectory(SavesDir(installPath));
        var flag = Path.Combine(SavesDir(installPath), ".launching");
        if (!File.Exists(flag)) File.WriteAllText(flag, "");
    }

    /// <summary>按空白切分参数，尊重双引号。vmparams 首 token "java.exe" 会被保留，由调用方决定去留。</summary>
    public static List<string> Tokenize(string text)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        foreach (var ch in text)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
            }
            else if (char.IsWhiteSpace(ch) && !inQuote)
            {
                if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
            }
            else
            {
                sb.Append(ch);
            }
        }
        if (sb.Length > 0) result.Add(sb.ToString());
        return result;
    }

    /// <summary>从 vmparams 读取当前 -Xmx / -Xms（单位换算成 MB）。</summary>
    public static (int XmxMb, int XmsMb) ReadMemoryMb(string vmparamsText)
    {
        int xmx = 0, xms = 0;
        foreach (var arg in Tokenize(vmparamsText))
        {
            if (TryParseMemory(arg, "-Xmx", out var v)) xmx = v;
            else if (TryParseMemory(arg, "-Xms", out var v2)) xms = v2;
        }
        return (xmx, xms);
    }

    /// <summary>构建启动参数：去掉首 token "java.exe"，替换/追加 -Xmx/-Xms。</summary>
    public static List<string> BuildArgs(string vmparamsText, int xmxMb, int xmsMb)
    {
        var args = Tokenize(vmparamsText);
        if (args.Count > 0 && args[0].Equals("java.exe", StringComparison.OrdinalIgnoreCase))
            args.RemoveAt(0);
        ReplaceOrAdd(args, "-Xmx", $"-Xmx{xmxMb}m");
        ReplaceOrAdd(args, "-Xms", $"-Xms{xmsMb}m");
        return args;
    }

    /// <summary>构建完整的启动参数（含可选的跳过启动器 -D 参数）。纯函数，便于测试。</summary>
    public static List<string> BuildLaunchArgs(string installPath, int xmxMb, int xmsMb,
        bool skipLauncher, string resolution, bool fullscreen, bool sound,
        IReadOnlyList<string>? extraArgs = null)
    {
        string vmparamsText = File.ReadAllText(VmparamsPath(installPath));
        var args = BuildArgs(vmparamsText, xmxMb, xmsMb);

        if (extraArgs is { Count: > 0 })
        {
            int mainIdx = args.Count - 1; // 主类（com.fs.starfarer.StarfarerLauncher）位于最后
            for (int i = extraArgs.Count - 1; i >= 0; i--)
                args.Insert(mainIdx, extraArgs[i]);
        }

        if (skipLauncher)
        {
            args.Insert(0, "-DstartSound=" + (sound ? "true" : "false"));
            args.Insert(0, "-DstartFS=" + (fullscreen ? "true" : "false"));
            args.Insert(0, "-DstartRes=" + resolution);
            args.Insert(0, "-DlaunchDirect=true");
        }
        return args;
    }

    /// <summary>启动游戏进程。skipLauncher=true 时直接进入游戏主菜单。</summary>
    public static Process Launch(string installPath, int xmxMb, int xmsMb,
        bool skipLauncher = true, string resolution = "1920x1080", bool fullscreen = false, bool sound = true,
        ProcessPriorityClass priority = ProcessPriorityClass.Normal, IReadOnlyList<string>? extraArgs = null)
    {
        if (skipLauncher) SetLaunchFlag(installPath);
        var args = BuildLaunchArgs(installPath, xmxMb, xmsMb, skipLauncher, resolution, fullscreen, sound, extraArgs);

        var psi = new ProcessStartInfo
        {
            FileName = JavaExePath(installPath),
            WorkingDirectory = CoreDir(installPath),
            UseShellExecute = false,
        };
        psi.Arguments = CommandLine.Quote(args);

        var p = Process.Start(psi) ?? throw new InvalidOperationException("游戏进程未能启动");
        if (priority != ProcessPriorityClass.Normal)
        {
            try { p.PriorityClass = priority; } catch { /* 设置优先级失败不致命 */ }
        }
        return p;
    }

    private static void ReplaceOrAdd(List<string> args, string flag, string replacement)
    {
        int idx = args.FindIndex(a => a.StartsWith(flag, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) args[idx] = replacement;
        else args.Add(replacement);
    }

    private static bool TryParseMemory(string arg, string flag, out int mb)
    {
        mb = 0;
        if (!arg.StartsWith(flag, StringComparison.OrdinalIgnoreCase)) return false;
        var rest = arg.Substring(flag.Length);
        var m = MemoryRegex.Match(rest);
        if (!m.Success) return false;

        long value = long.Parse(m.Groups[1].Value);
        char unit = m.Groups[2].Success ? m.Groups[2].Value[0] : 'm';
        mb = unit switch
        {
            'k' or 'K' => (int)(value / 1024),
            'g' or 'G' => (int)(value * 1024),
            _ => (int)value,
        };
        return true;
    }

    private static readonly Regex MemoryRegex = new(@"^(\d+)([kKmMgG])?$", RegexOptions.Compiled);
}
