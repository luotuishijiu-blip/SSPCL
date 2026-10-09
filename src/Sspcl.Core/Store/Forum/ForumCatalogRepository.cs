using System.Text.Json;

namespace Sspcl.Core.Store.Forum;

public sealed class ForumCacheEntry<T>
{
    public T? Data { get; set; }
    public DateTimeOffset FetchedUtc { get; set; }
    public DateTimeOffset AttemptedUtc { get; set; }
    public string? Error { get; set; }
}

/// <summary>可持久化目录快照。元数据和 MOD 索引独立过期。</summary>
public sealed class ForumCatalog
{
    public int Schema { get; set; } = 1;
    public string Source { get; set; } = "";
    public ForumCacheEntry<List<ForumMod>> Mods { get; set; } = new();
    public ForumCacheEntry<List<string>> Versions { get; set; } = new();
    public ForumCacheEntry<Dictionary<string, string>> Categories { get; set; } = new();
    public ForumCacheEntry<Dictionary<string, string>> Languages { get; set; } = new();
}

public sealed class ForumCatalogResult
{
    public ForumCatalog Catalog { get; }
    public bool IsStale { get; }
    public string? Warning { get; }
    internal ForumCatalogResult(ForumCatalog catalog, bool stale, string? warning)
    { Catalog = catalog; IsStale = stale; Warning = warning; }
}

/// <summary>缓存优先的数据层；同一实例合并刷新，失败保留旧数据并退避。</summary>
public sealed class ForumCatalogRepository
{
    public static readonly TimeSpan IndexLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MetadataLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(2);
    private readonly ForumApiClient _api;
    private readonly string _path;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ForumCatalog? _catalog;
    private string? _diskWarning;

    public ForumCatalogRepository(ForumApiClient api, string cacheDirectory,
        Func<DateTimeOffset>? utcNow = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _path = Path.Combine(Path.GetFullPath(cacheDirectory), "catalog-v1.json");
        _now = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<ForumCatalog?> GetCachedAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ReadAsync(cancellationToken).ConfigureAwait(false);
            return _catalog!.Mods.Data == null ? null : _catalog;
        }
        finally { _gate.Release(); }
    }

    // 手动刷新只强制刷新目录；元数据仍遵循 24 小时有效期。
    public async Task<ForumCatalogResult> RefreshAsync(bool force = false,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ReadAsync(cancellationToken).ConfigureAwait(false);
            var catalog = _catalog!;
            var now = _now();
            // 仅在确实过期时创建 HTTP 任务，打开有效缓存不发送任何请求。
            var mods = RefreshEntry(catalog.Mods, IndexLifetime, force, () => _api.GetModsAsync(false, cancellationToken), now, cancellationToken);
            var versions = RefreshEntry(catalog.Versions, MetadataLifetime, false, () => _api.GetGameVersionsAsync(cancellationToken), now, cancellationToken);
            var categories = RefreshEntry(catalog.Categories, MetadataLifetime, false, () => _api.GetCategoriesAsync(cancellationToken), now, cancellationToken);
            var languages = RefreshEntry(catalog.Languages, MetadataLifetime, false, () => _api.GetLanguagesAsync(cancellationToken), now, cancellationToken);
            var changed = await Task.WhenAll(mods, versions, categories, languages).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (changed.Any(value => value)) await WriteAsync(catalog, cancellationToken).ConfigureAwait(false);
            if (catalog.Mods.Data == null)
                throw new InvalidOperationException("暂无可用目录，请稍后重试。" + catalog.Mods.Error);
            var errors = new[] { catalog.Mods.Error, catalog.Versions.Error, catalog.Categories.Error, catalog.Languages.Error, _diskWarning }
                .Where(error => !string.IsNullOrWhiteSpace(error)).Distinct();
            var warning = string.Join("；", errors);
            return new ForumCatalogResult(catalog, !Fresh(catalog.Mods.FetchedUtc, IndexLifetime, now), warning.Length == 0 ? null : warning);
        }
        finally { _gate.Release(); }
    }

    private async Task<bool> RefreshEntry<T>(ForumCacheEntry<T> entry, TimeSpan lifetime, bool force,
        Func<Task<T>> fetch, DateTimeOffset now, CancellationToken token)
    {
        if (entry.Data != null && !force && Fresh(entry.FetchedUtc, lifetime, now)) return false;
        var cooldown = entry.Error == null ? RefreshCooldown : FailureBackoff;
        if (Fresh(entry.AttemptedUtc, cooldown, now)) return false;
        token.ThrowIfCancellationRequested();
        try
        {
            var data = await fetch().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            entry.Data = data;
            entry.FetchedUtc = now;
            entry.AttemptedUtc = now;
            entry.Error = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is JsonException || ex is OperationCanceledException)
        {
            entry.AttemptedUtc = now;
            entry.Error = "论坛暂时不可用，保留已有目录：" + ex.Message;
        }
        return true;
    }

    private static bool Fresh(DateTimeOffset timestamp, TimeSpan lifetime, DateTimeOffset now) =>
        timestamp != default && now >= timestamp && now - timestamp < lifetime;

    private async Task ReadAsync(CancellationToken token)
    {
        if (_catalog != null) return;
        try
        {
            if (File.Exists(_path))
            {
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, true);
                if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("目录缓存超过大小限制。");
                var cached = await JsonSerializer.DeserializeAsync<ForumCatalog>(stream, cancellationToken: token).ConfigureAwait(false);
                if (cached != null && cached.Schema == 1 && cached.Source == _api.CacheSource &&
                    cached.Mods != null && cached.Versions != null && cached.Categories != null && cached.Languages != null &&
                    (cached.Mods.Data == null || cached.Mods.Data.All(ValidMod)))
                    _catalog = cached;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
        { _diskWarning = "本地缓存无法读取，已改为重新加载。"; }
        _catalog ??= new ForumCatalog { Source = _api.CacheSource };
    }

    private static bool ValidMod(ForumMod mod) => mod != null && mod.Id != null && mod.ChineseName != null &&
        mod.Authors != null && mod.Translators != null && mod.GameVersions != null && mod.DependencyNames != null &&
        mod.ConflictNames != null && mod.PublishUrls != null && mod.AdminNotes != null && mod.Thread != null &&
        (mod.Releases == null || mod.Releases.All(release => release != null));

    private async Task WriteAsync(ForumCatalog catalog, CancellationToken token)
    {
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await JsonSerializer.SerializeAsync(stream, catalog, cancellationToken: token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            if (File.Exists(_path)) File.Replace(temporary, _path, null);
            else File.Move(temporary, _path);
            _diskWarning = null;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        { _diskWarning = "目录已加载，但无法写入本地缓存；请检查磁盘权限或空间。"; }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
