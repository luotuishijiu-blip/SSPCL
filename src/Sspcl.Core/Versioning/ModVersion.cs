using System.Text.RegularExpressions;

namespace Sspcl.Core.Versioning;

/// <summary>mod 版本，归一化为 major.minor.patch 三元组以便比较。</summary>
public readonly partial record struct ModVersion(int Major, int Minor, int Patch) : IComparable<ModVersion>
{
    public static readonly ModVersion Unspecified = new(0, 0, 0);

    public int CompareTo(ModVersion other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        return Patch.CompareTo(other.Patch);
    }

    public static bool operator <(ModVersion a, ModVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(ModVersion a, ModVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(ModVersion a, ModVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(ModVersion a, ModVersion b) => a.CompareTo(b) >= 0;

    /// <summary>从字符串取前三个数字组（'0.5.7c-fix' → 0.5.7）。</summary>
    public static ModVersion FromString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Unspecified;
        var nums = Number.Matches(raw).Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
        return new ModVersion(
            nums.Length > 0 ? nums[0] : 0,
            nums.Length > 1 ? nums[1] : 0,
            nums.Length > 2 ? nums[2] : 0);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    private static readonly Regex Number = new(@"\d+", RegexOptions.Compiled);
}
