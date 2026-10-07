using System.IO;
using System.Text.Json;
using Sspcl.Core.Install;

namespace Sspcl.App.Services;

/// <summary>sspcl 的应用配置（%APPDATA%\sspcl\config.json）：安装列表 + 当前安装 + 按安装的内存默认值。</summary>
public sealed class AppSettings
{
    public string CurrentInstallPath { get; set; } = "";
    public List<string> InstallPaths { get; set; } = new();
    public Dictionary<string, MemorySettings> Memory { get; set; } = new();
    public Dictionary<string, Dictionary<string, ModMeta>> ModMeta { get; set; } = new();
    public Dictionary<string, string> LocalizationSeen { get; set; } = new();
    public Dictionary<string, string> InstallNotes { get; set; } = new();

    /// <summary>启动后最小化到托盘。</summary>
    public bool LaunchMinimize { get; set; } = true;
    /// <summary>默认跳过游戏自带启动器。</summary>
    public bool SkipLauncherDefault { get; set; } = true;
    /// <summary>游戏进程优先级：Normal / AboveNormal / High。</summary>
    public string ProcessPriority { get; set; } = "Normal";
    /// <summary>隐藏 Mod 管理中的兼容性警告（缺前置/重复 id/版本超标）。</summary>
    public bool HideModWarnings { get; set; }
    /// <summary>默认内存：true=自动（跟随 vmparams），false=自定义。</summary>
    public bool DefaultMemoryAuto { get; set; } = true;
    /// <summary>自定义默认内存 -Xmx（MB）。</summary>
    public int DefaultMemoryMb { get; set; }
    /// <summary>自定义默认内存 -Xms（MB）。</summary>
    public int DefaultXmsMb { get; set; }
    /// <summary>百宝箱：玩家自行添加的工具。</summary>
    public List<ToolEntry> Tools { get; set; } = new();
    /// <summary>AI 配装：API 地址（完整，含 /chat/completions）。</summary>
    public string AiEndpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    /// <summary>AI 配装：API Key。</summary>
    public string AiKey { get; set; } = "";
    /// <summary>AI 配装：模型名称。</summary>
    public string AiModel { get; set; } = "gpt-4o-mini";
    /// <summary>反馈接收地址（Discord Webhook 或任意 POST 接口）。</summary>
    public string FeedbackUrl { get; set; } = "";

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sspcl");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppSettings Load()
    {
        AppSettings? settings = null;
        if (File.Exists(ConfigPath))
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath));
            }
            catch { /* 配置损坏则重建 */ }
        }
        settings ??= new AppSettings();

        // 首次运行：登记本机两套已知安装（明确的已知路径，非全盘扫描）
        var known = new[] { @"D:\Starsector", @"F:\Starsector" };
        foreach (var c in known)
            if (Directory.Exists(c) && !settings.InstallPaths.Contains(c))
                settings.InstallPaths.Add(c);

        settings.InstallPaths.RemoveAll(p => !Directory.Exists(p));

        if (string.IsNullOrEmpty(settings.CurrentInstallPath)
            || !settings.InstallPaths.Contains(settings.CurrentInstallPath))
        {
            settings.CurrentInstallPath = settings.InstallPaths.FirstOrDefault() ?? "";
        }

        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public MemorySettings GetMemory(string installPath) =>
        Memory.TryGetValue(installPath, out var m) ? m : new MemorySettings();

    public void SetMemory(string installPath, int xmxMb, int xmsMb) =>
        Memory[installPath] = new MemorySettings { XmxMb = xmxMb, XmsMb = xmsMb };

    public ModMeta GetModMeta(string installPath, string modId)
    {
        if (ModMeta.TryGetValue(installPath, out var map) && map.TryGetValue(modId, out var meta))
            return meta;
        return new ModMeta();
    }

    public void SaveModMeta(string installPath, string modId, ModMeta meta)
    {
        if (!ModMeta.TryGetValue(installPath, out var map))
            ModMeta[installPath] = map = new Dictionary<string, ModMeta>();
        map[modId] = meta;
    }

    public string GetLocalizationSeen(string installPath) =>
        LocalizationSeen.TryGetValue(installPath, out var v) ? v : "";

    public void SetLocalizationSeen(string installPath, string version) =>
        LocalizationSeen[installPath] = version;

    public string GetInstallNote(string installPath) =>
        InstallNotes.TryGetValue(installPath, out var n) ? n : "";

    public void SetInstallNote(string installPath, string note) =>
        InstallNotes[installPath] = note;
}

public sealed class ModMeta
{
    public bool Favorite { get; set; }
    public string Note { get; set; } = "";
    public string Tags { get; set; } = "";
}

public sealed class MemorySettings
{
    public int XmxMb { get; set; } = 8192;
    public int XmsMb { get; set; } = 8192;
}
