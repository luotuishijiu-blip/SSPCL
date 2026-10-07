using System.Text;

namespace Sspcl.Core.Utility;

/// <summary>把参数列表拼成 Windows 命令行字符串（供 netstandard2.0 无 ArgumentList 时使用）。</summary>
public static class CommandLine
{
    public static string Quote(IEnumerable<string> args)
    {
        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
                sb.Append(a);
            else
            {
                sb.Append('"');
                foreach (var c in a)
                {
                    if (c == '"') sb.Append("\\\"");
                    else sb.Append(c);
                }
                sb.Append('"');
            }
        }
        return sb.ToString();
    }
}
