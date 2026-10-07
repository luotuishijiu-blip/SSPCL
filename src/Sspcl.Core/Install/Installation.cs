using Sspcl.Core.Versioning;

namespace Sspcl.Core.Install;

/// <summary>一份远行星号安装目录的检测结果。</summary>
public sealed record Installation
{
    public required string Path { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();

    /// <summary>游戏核心版本，来自 starfarer_obf.jar 的 Version.class 常量。</summary>
    public string Version { get; init; } = "";
    /// <summary>汉化包版本号（localization_version.json 的 version 字段）。</summary>
    public string LocalizationPackageVersion { get; init; } = "";
    /// <summary>汉化包声明的游戏版本（game_version 字段）。</summary>
    public string LocalizationVersion { get; init; } = "";

    public int ModCount { get; init; }
    public int EnabledModCount { get; init; }
    public int SaveCount { get; init; }

    public GameVersion ParsedVersion => GameVersion.Parse(Version);
}
