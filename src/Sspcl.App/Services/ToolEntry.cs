namespace Sspcl.App.Services;

/// <summary>百宝箱里玩家自行添加的一个工具。</summary>
public sealed class ToolEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public string WorkDir { get; set; } = "";
    /// <summary>是否已部署到主页右下角。</summary>
    public bool Pinned { get; set; }
}
