using System.Net.Http;
using System.Text.Json;

namespace Sspcl.Core.Store.Forum;

/// <summary>论坛测试 API 接入层。HttpClient 的认证、超时及生命周期由调用方管理。</summary>
public sealed class ForumApiClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseAddress;
    public string CacheSource => _baseAddress.AbsoluteUri;

    public ForumApiClient(HttpClient http, Uri baseAddress)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));
        if (!baseAddress.IsAbsoluteUri || !IsWebUri(baseAddress) ||
            baseAddress.Query.Length > 0 || baseAddress.Fragment.Length > 0)
            throw new ArgumentException("API 基础地址必须为不带查询参数和片段的 HTTP(S) 地址。", nameof(baseAddress));
        _baseAddress = new Uri(baseAddress.AbsoluteUri.TrimEnd('/') + "/");
    }

    public Task<List<ForumMod>> GetModsAsync(bool includeModding = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<List<ForumMod>>("mods?include_modding=" + (includeModding ? "true" : "false"), cancellationToken);

    public Task<List<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<string>>("meta/game_versions", cancellationToken);

    public Task<Dictionary<string, string>> GetLanguagesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<Dictionary<string, string>>("meta/mod_languages", cancellationToken);

    public Task<Dictionary<string, string>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<Dictionary<string, string>>("meta/mod_categories", cancellationToken);

    // OpenAPI 未限定 status 的返回结构，保留原始 JSON。
    public Task<JsonElement> GetStatusAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>("status", cancellationToken);

    /// <summary>只有服务端允许直连且指定发布包有有效地址时，才返回下载地址。</summary>
    public Uri? GetDownloadUri(ForumMod mod, ForumModRelease release)
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));
        if (release == null) throw new ArgumentNullException(nameof(release));
        if (!mod.AllowDirectDownload || string.IsNullOrWhiteSpace(release.DownloadUrl)) return null;
        return Uri.TryCreate(_baseAddress, release.DownloadUrl, out var uri) && IsWebUri(uri) ? uri : null;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseAddress, path));
        request.Headers.UserAgent.ParseAdd(ForumClientIdentity.UserAgent);
        using var response = await _http.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result is null) throw new JsonException("论坛 API 返回了空响应。");
        return result;
    }

    private static bool IsWebUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
}
