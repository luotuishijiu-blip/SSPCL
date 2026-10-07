using System.Text.RegularExpressions;

namespace Sspcl.Core.Versioning;

/// <summary>
/// 游戏版本，形如 0.98a-RC8 → (0, 98, 1, 8)。
/// 字母段 a=1, b=2…；无字母记 0；RC 无则记 0。
/// </summary>
public readonly partial record struct GameVersion(int Major, int Minor, int Letter, int Rc) : IComparable<GameVersion>
{
    public static readonly GameVersion Unknown = new(0, 0, 0, 0);

    public int CompareTo(GameVersion other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Letter.CompareTo(other.Letter);
        if (c != 0) return c;
        return Rc.CompareTo(other.Rc);
    }

    public static bool operator <(GameVersion a, GameVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(GameVersion a, GameVersion b) => a.CompareTo(b) > 0;

    public static GameVersion Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Unknown;
        var m = Base.Match(raw);
        if (!m.Success) return Unknown;
        int major = int.Parse(m.Groups[1].Value);
        int minor = int.Parse(m.Groups[2].Value);
        int letter = m.Groups[3].Success ? char.ToLowerInvariant(m.Groups[3].Value[0]) - 'a' + 1 : 0;
        var rc = RcMatch.Match(raw);
        int rcNum = rc.Success ? int.Parse(rc.Groups[1].Value) : 0;
        return new GameVersion(major, minor, letter, rcNum);
    }

    public override string ToString()
    {
        string letter = Letter > 0 ? ((char)('a' + Letter - 1)).ToString() : "";
        string rc = Rc > 0 ? $"-RC{Rc}" : "";
        return $"{Major}.{Minor}{letter}{rc}";
    }

    private static readonly Regex Base = new(@"(\d+)\.(\d+)([a-zA-Z])?", RegexOptions.Compiled);
    private static readonly Regex RcMatch = new(@"[rR][cC]\s*(\d+)", RegexOptions.Compiled);
}
