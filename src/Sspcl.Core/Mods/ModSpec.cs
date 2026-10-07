using Sspcl.Core.Versioning;

namespace Sspcl.Core.Mods;

/// <summary>dependencies 数组里的单项（id + 可选的 name / version 约束）。</summary>
public sealed record ModDependency(string Id, string Name = "", string Version = "");

/// <summary>一个已解析的 mod 目录。</summary>
public sealed record ModSpec
{
    public required string Folder { get; init; }
    public required string Path { get; init; }

    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();

    /// <summary>version 字段的原始形态：str / dict / int / float / ""（未声明）。</summary>
    public string VersionShape { get; init; } = "";
    public string VersionRaw { get; init; } = "";
    public ModVersion Version { get; init; } = ModVersion.Unspecified;

    public string GameVersionRaw { get; init; } = "";
    public GameVersion GameVersion { get; init; } = GameVersion.Unknown;

    public bool Utility { get; init; }
    public bool TotalConversion { get; init; }
    public IReadOnlyList<string> Jars { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = Array.Empty<ModDependency>();
    public string ModPlugin { get; init; } = "";

    /// <summary>非空表示解析失败或缺少 id。</summary>
    public string? Error { get; init; }
}
