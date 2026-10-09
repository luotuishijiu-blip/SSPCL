using System.Diagnostics;

namespace Sspcl.Core.Launch;

public sealed class GameMemoryUsage
{
    public int ProcessCount { get; internal set; }
    public long WorkingSetBytes { get; internal set; }
    public long PrivateBytes { get; internal set; }
    public bool AccessDenied { get; internal set; }
}

public static class GameMemoryMonitor
{
    public static GameMemoryUsage Read(string gameDir)
    {
        string root = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var usage = new GameMemoryUsage();
        foreach (string name in new[] { "java", "javaw", "starsector" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        string? executable = process.MainModule?.FileName;
                        if (executable == null || !Path.GetFullPath(executable).StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                        process.Refresh();
                        usage.ProcessCount++;
                        usage.WorkingSetBytes += process.WorkingSet64;
                        usage.PrivateBytes += process.PrivateMemorySize64;
                    }
                    catch (System.ComponentModel.Win32Exception) { usage.AccessDenied = true; }
                    catch (InvalidOperationException) { /* 已退出。 */ }
                }
            }
        }
        return usage;
    }
}
