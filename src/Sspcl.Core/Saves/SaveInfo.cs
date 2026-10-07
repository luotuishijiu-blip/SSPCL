namespace Sspcl.Core.Saves;

/// <summary>从 descriptor.xml 解析出的存档信息。</summary>
public sealed record SaveInfo
{
    public required string Folder { get; init; }
    public required string Path { get; init; }

    public string CharacterName { get; init; } = "";
    public int CharacterLevel { get; init; }
    public string GameVersion { get; init; } = "";
    public string SaveDate { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public bool IronMode { get; init; }
    public bool Compressed { get; init; }

    public IReadOnlyList<string> EnabledModIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllModsEverEnabled { get; init; } = Array.Empty<string>();
    public long DescriptorSize { get; init; }
}
