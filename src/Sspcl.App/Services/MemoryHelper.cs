namespace Sspcl.App.Services;

public static class MemoryHelper
{
    /// <summary>自动内存（MB）：主机最大内存 ÷ 2 − 2GB，下限 4GB、上限 64GB。</summary>
    public static int AutoMemoryMb()
    {
        try
        {
            var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
            double totalGb = ci.TotalPhysicalMemory / 1024.0 / 1024 / 1024;
            int mb = (int)Math.Round((totalGb / 2 - 2) * 1024);
            return Math.Clamp(mb, 4096, 65536);
        }
        catch { return 8192; }
    }
}
