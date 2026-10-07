namespace Sspcl.App.Services;

public static class ToolRunner
{
    /// <summary>运行工具，返回错误信息（空串表示成功）。</summary>
    public static string Run(ToolEntry tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Path)) return "工具路径为空";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(tool.Path) { UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(tool.Args)) psi.Arguments = tool.Args;
            if (!string.IsNullOrWhiteSpace(tool.WorkDir)) psi.WorkingDirectory = tool.WorkDir;
            System.Diagnostics.Process.Start(psi);
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
