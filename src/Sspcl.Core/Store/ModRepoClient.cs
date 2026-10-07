using System.Net.Http;
using System.Text.Json;

namespace Sspcl.Core.Store;

/// <summary>StarsectorModRepo 索引中的一条 Mod。</summary>
public sealed class StoreItem
{
    public string Name { get; set; } = "";
    public string GameVersionReq { get; set; } = "";
    public List<string> Authors { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ForumUrl { get; set; }
    public string? DownloadPageUrl { get; set; }
    public string? DirectDownloadUrl { get; set; }

    public string HomeUrl => DownloadPageUrl ?? ForumUrl ?? "";

    public string Initial
    {
        get
        {
            var trimmed = Name.TrimStart('-', ' ');
            return string.IsNullOrEmpty(trimmed) ? "?" : trimmed.Substring(0, 1).ToUpperInvariant();
        }
    }

    public string Meta => string.Join(" · ",
        new[]
        {
            string.Join(", ", Authors),
            string.Join(", ", Categories),
            "游戏 " + GameVersionReq,
        }.Where(s => !string.IsNullOrEmpty(s)));

    public bool HasDirectDownload =>
        !string.IsNullOrEmpty(DirectDownloadUrl) && IsArchiveUrl(DirectDownloadUrl);

    private static bool IsArchiveUrl(string url)
    {
        int q = url.IndexOf('?');
        string path = q >= 0 ? url.Substring(0, q) : url;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".zip" or ".7z" or ".rar";
    }
}

/// <summary>拉取 wispborne/StarsectorModRepo 社区索引（TriOS 同款数据源）。</summary>
public static class ModRepoClient
{
    public const string RepoUrl =
        "https://raw.githubusercontent.com/wispborne/StarsectorModRepo/refs/heads/main/ModRepo.json";

    public static async Task<List<StoreItem>> FetchAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("sspcl/1.0");
        string json = await http.GetStringAsync(RepoUrl);

        using var doc = JsonDocument.Parse(json);
        var items = new List<StoreItem>();
        if (!doc.RootElement.TryGetProperty("items", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var e in arr.EnumerateArray())
        {
            var item = new StoreItem
            {
                Name = GetString(e, "name"),
                GameVersionReq = GetString(e, "gameVersionReq"),
                Summary = GetString(e, "summary"),
                Description = GetString(e, "description"),
            };
            if (e.TryGetProperty("authorsList", out var al) && al.ValueKind == JsonValueKind.Array)
                foreach (var x in al.EnumerateArray()) item.Authors.Add(x.GetString() ?? "");
            if (e.TryGetProperty("categories", out var c) && c.ValueKind == JsonValueKind.Array)
                foreach (var x in c.EnumerateArray()) item.Categories.Add(x.GetString() ?? "");
            if (e.TryGetProperty("urls", out var urls) && urls.ValueKind == JsonValueKind.Object)
            {
                item.ForumUrl = GetOptString(urls, "Forum");
                item.DownloadPageUrl = GetOptString(urls, "DownloadPage");
                item.DirectDownloadUrl = GetOptString(urls, "DirectDownload");
            }
            items.Add(item);
        }
        return items;
    }

    /// <summary>把直链下载到临时文件（保留正确扩展名供解压）。</summary>
    public static async Task<string> DownloadToTempAsync(string url)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("sspcl/1.0");
        byte[] bytes = await http.GetByteArrayAsync(url);
        string ext = GuessExtension(url);
        string path = Path.Combine(Path.GetTempPath(), "sspcl-dl-" + Guid.NewGuid().ToString("N") + ext);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string GetString(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string? GetOptString(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string GuessExtension(string url)
    {
        int q = url.IndexOf('?');
        string path = q >= 0 ? url.Substring(0, q) : url;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".zip" or ".7z" or ".rar" ? ext : ".zip";
    }
}
