using System.Text;

namespace Sspcl.Core.Logs;

/// <summary>读取 starsector.log 的尾部、按关键字/错误过滤（只读，不解析存档）。</summary>
public static class GameLog
{
    public static string LogPath(string gameDir) => Path.Combine(gameDir, "starsector-core", "starsector.log");

    public static string ReadTail(string gameDir, int maxLines = 2000, string? filter = null, bool errorsOnly = false)
    {
        string path = LogPath(gameDir);
        if (!File.Exists(path)) return "（未找到 starsector.log）";

        string text = Decode(File.ReadAllBytes(path));
        var lines = text.Split('\n');
        var tail = lines.Skip(Math.Max(0, lines.Length - maxLines));

        if (errorsOnly)
            tail = tail.Where(IsErrorLine);
        else if (!string.IsNullOrWhiteSpace(filter))
            tail = tail.Where(l => l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

        return string.Join("\n", tail);
    }

    public static bool IsErrorLine(string line) =>
        line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0
        || line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
        || line.IndexOf("FATAL", StringComparison.OrdinalIgnoreCase) >= 0
        || line.IndexOf("Caused by", StringComparison.OrdinalIgnoreCase) >= 0
        || line.IndexOf("SEVERE", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>优先严格 UTF-8；失败则尝试系统 ANSI 代码页（GBK 等），再兜底为替换式 UTF-8。</summary>
    private static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return Encoding.GetEncoding(0).GetString(bytes);
            }
            catch
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: false).GetString(bytes);
            }
        }
    }
}
