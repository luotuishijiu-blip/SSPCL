using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Sspcl.Core.Jsonc;

/// <summary>
/// 远行星号 mod_info.json 的宽容解析器。
/// 对标 scripts/jsonc_dialect.py 的行为：
///   1) 字符串外剥离 # / #// / // 与 /* */ 注释，字符串内原样保留
///   2) 根对象结束后截断游离文本
///   3) 容忍尾随逗号
///   4) 单引号字符串折叠为双引号
///   5) 尝试严格解析，失败再给未加引号的键补引号
/// </summary>
public static partial class JsoncParser
{
    private static readonly Regex TrailingComma = new(@",(\s*[}\]])", RegexOptions.Compiled);
    private static readonly Regex BareKey = new(@"([{,]\s*)([A-Za-z_][A-Za-z0-9_.\-]*)(\s*:)", RegexOptions.Compiled);

    public static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0, n = text.Length;
        char quote = '\0';
        while (i < n)
        {
            char ch = text[i];
            if (quote != '\0')
            {
                sb.Append(ch);
                if (ch == '\\' && i + 1 < n)
                {
                    sb.Append(text[i + 1]);
                    i += 2;
                    continue;
                }
                if (ch == quote) quote = '\0';
                i++;
                continue;
            }
            if (ch is '"' or '\'')
            {
                quote = ch;
                sb.Append(ch);
                i++;
                continue;
            }
            if (ch == '#')
            {
                while (i < n && text[i] is not ('\r' or '\n')) i++;
                continue;
            }
            if (ch == '/' && i + 1 < n && text[i + 1] == '/')
            {
                while (i < n && text[i] is not ('\r' or '\n')) i++;
                continue;
            }
            if (ch == '/' && i + 1 < n && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
                continue;
            }
            sb.Append(ch);
            i++;
        }
        return sb.ToString();
    }

    public static string TruncateAfterRoot(string text)
    {
        int depth = 0, i = 0, n = text.Length;
        char quote = '\0';
        while (i < n)
        {
            char ch = text[i];
            if (quote != '\0')
            {
                if (ch == '\\') { i += 2; continue; }
                if (ch == quote) quote = '\0';
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
            }
            else if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(0, i + 1);
            }
            i++;
        }
        return text;
    }

    public static string NormalizeSingleQuotes(string text)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0, n = text.Length;
        char quote = '\0';
        while (i < n)
        {
            char ch = text[i];
            if (quote == '"')
            {
                sb.Append(ch);
                if (ch == '\\' && i + 1 < n) { sb.Append(text[i + 1]); i += 2; continue; }
                if (ch == '"') quote = '\0';
                i++;
                continue;
            }
            if (quote == '\'')
            {
                if (ch == '\\' && i + 1 < n) { sb.Append(ch); sb.Append(text[i + 1]); i += 2; continue; }
                if (ch == '\'') { quote = '\0'; sb.Append('"'); i++; continue; }
                sb.Append(ch == '"' ? "\\\"" : ch.ToString());
                i++;
                continue;
            }
            if (ch == '"') { quote = '"'; }
            else if (ch == '\'') { quote = '\''; sb.Append('"'); i++; continue; }
            sb.Append(ch);
            i++;
        }
        return sb.ToString();
    }

    public static string QuoteBareKeys(string text) => BareKey.Replace(text, "$1\"$2\"$3");

    /// <summary>把真实世界的 mod_info.json 解析为 JsonNode；根对象之外的异常抛 JsonException。</summary>
    public static JsonNode? Parse(string text)
    {
        string clean = StripComments(text);
        clean = TruncateAfterRoot(clean);
        clean = TrailingComma.Replace(clean, "$1");
        clean = NormalizeSingleQuotes(clean);
        try
        {
            return JsonNode.Parse(clean);
        }
        catch (System.Text.Json.JsonException)
        {
            return JsonNode.Parse(QuoteBareKeys(clean));
        }
    }
}
