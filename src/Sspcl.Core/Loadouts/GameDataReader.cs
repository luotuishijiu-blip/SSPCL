using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Sspcl.Core.Jsonc;

namespace Sspcl.Core.Loadouts;

internal static class GameDataReader
{
    public static JsonObject Json(string path)
    {
        string raw = File.ReadAllText(path);
        try { return JsoncParser.Parse(raw) as JsonObject ?? throw new InvalidDataException("根节点不是对象。"); }
        catch (System.Text.Json.JsonException)
        {
            string clean = JsoncParser.NormalizeSingleQuotes(JsoncParser.TruncateAfterRoot(JsoncParser.StripComments(raw)));
            clean = Regex.Replace(clean, "\"(?:\\\\.|[^\"\\\\])*\"|;", match => match.Value == ";" ? "," : match.Value);
            // org.json 风格文件允许 +100、.8、017 和 Java float 后缀。仅正规化字符串外的数字。
            clean = Regex.Replace(clean, "\"(?:\\\\.|[^\"\\\\])*\"|(?<![A-Za-z0-9_.])[-+]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?[fFdD]?(?![A-Za-z0-9_.])", match =>
                match.Value.StartsWith("\"", StringComparison.Ordinal) ? match.Value :
                double.Parse(match.Value.TrimEnd('f', 'F', 'd', 'D'), NumberStyles.Float, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
            // 游戏文件允许未加引号的枚举（如 RENDER_BARREL_BELOW）；保护字符串中的词与 #。
            clean = Regex.Replace(clean, "\"(?:\\\\.|[^\"\\\\])*\"|(?<![0-9.])[A-Za-z_][A-Za-z0-9_.-]*", match =>
                match.Value.StartsWith("\"", StringComparison.Ordinal) || match.Value == "true" || match.Value == "false" || match.Value == "null" ? match.Value : "\"" + match.Value + "\"");
            return JsoncParser.Parse(clean) as JsonObject ?? throw new InvalidDataException("根节点不是对象。");
        }
    }
    public static string Text(JsonNode? node, string fallback = "") => node?.ToString() ?? fallback;
    public static double Number(JsonNode? node, double fallback = 0) => double.TryParse(Text(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : fallback;
    public static IEnumerable<string> Strings(JsonNode? node) => node is JsonArray array ? array.Select(n => Text(n)).Where(s => s.Length > 0) : Enumerable.Empty<string>();

    public static IEnumerable<Dictionary<string, string>> Csv(string path)
    {
        if (!File.Exists(path)) yield break;
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        string[]? headers = null;
        foreach (var columns in CsvRecords(reader))
        {
            if (columns.Count == 0 || columns.All(string.IsNullOrWhiteSpace) || columns[0].TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
            if (headers == null) { headers = columns.Select(s => s.Trim()).ToArray(); continue; }
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++) if (headers[i].Length > 0) row[headers[i]] = i < columns.Count ? columns[i].Trim() : "";
            if (row.TryGetValue("id", out string? id) && !string.IsNullOrWhiteSpace(id)) yield return row;
        }
    }

    private static IEnumerable<List<string>> CsvRecords(TextReader reader)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        int code;
        while ((code = reader.Read()) >= 0)
        {
            char c = (char)code;
            if (c == '"')
            {
                if (quoted && reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                else quoted = !quoted;
            }
            else if (!quoted && c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (!quoted && (c == '\r' || c == '\n'))
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                row.Add(field.ToString()); field.Clear();
                yield return row; row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); yield return row; }
    }
    public static string Get(Dictionary<string, string> row, string key) => row.TryGetValue(key, out string? value) ? value : "";
    public static double GetNumber(Dictionary<string, string> row, string key) => double.TryParse(Get(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
}
