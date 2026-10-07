using System.Diagnostics;

namespace Sspcl.Core.Install;

/// <summary>检测游戏是否正在运行，用于在写入 enabled_mods.json 前加锁。</summary>
public static class GameProcessGuard
{
    public static bool IsGameRunning(string installPath)
    {
        try
        {
            if (Process.GetProcessesByName("starsector").Length > 0) return true;
        }
        catch { /* 进程查询失败不致命 */ }

        var jreBin = Path.Combine(installPath, "jre", "bin");
        try
        {
            foreach (var p in Process.GetProcessesByName("java"))
            {
                try
                {
                    var fileName = p.MainModule?.FileName ?? "";
                    if (fileName.StartsWith(jreBin, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { /* 单个进程可能无权访问 */ }
            }
        }
        catch { }

        return false;
    }
}
