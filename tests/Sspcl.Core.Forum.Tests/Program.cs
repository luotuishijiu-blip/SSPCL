using System.IO.Compression;
using System.Net;
using System.Text;
using Sspcl.Core.Mods;
using Sspcl.Core.Store.Forum;

// 无外部测试包的可重复契约检查：默认离线；--live 可读真实元数据，--live-download 可测试一个小附件。
var testRoot = Path.Combine(AppContext.BaseDirectory, "sspcl-forum-tests-" + Guid.NewGuid().ToString("N"));
var previousTmp = Environment.GetEnvironmentVariable("TMP");
var previousTemp = Environment.GetEnvironmentVariable("TEMP");
Directory.CreateDirectory(testRoot);
// 仅修改本测试进程的临时目录，解压也落在工作区内的独立目录。
Environment.SetEnvironmentVariable("TMP", testRoot);
Environment.SetEnvironmentVariable("TEMP", testRoot);
try
{
    await CheckContract();
    await CheckDownloads(testRoot);
    await CheckCatalogCache(testRoot);
    await CheckParallelDownloads(testRoot);
    await ModpackChecks.Run(testRoot);
    ActivationChecks.Run(testRoot);
    UpdateChecks.Run(testRoot);
    await LoadoutChecks.Run(testRoot);
    if (args.Contains("--live-loadout")) LoadoutChecks.Live("D:/Starsector");
    Console.WriteLine("Offline contract and download checks passed.");
    if (args.Contains("--live") || args.Contains("--live-download")) await CheckLive(testRoot, args.Contains("--live-download"));
    Console.WriteLine("PASS: forum API contract, download policy, archive formats, cancellation and installation.");
}
finally
{
    // 唯一由本进程创建的临时目录；不使用实际游戏或 MOD 池目录。
    Environment.SetEnvironmentVariable("TMP", previousTmp);
    Environment.SetEnvironmentVariable("TEMP", previousTemp);
    Directory.Delete(testRoot, recursive: true);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task MustThrow<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

static async Task CheckContract()
{
    using var http = new HttpClient(new FakeHandler(request =>
    {
        var path = request.RequestUri!.PathAndQuery;
        string json = path switch
        {
            "/api/mods?include_modding=false" or "/api/mods?include_modding=true" => """
                [
                  {"mod_id":"original","mod_info_type":"original","mod_name_cn":"原创","mod_releases":null},
                  {"mod_id":"translated","mod_info_type":"translated","mod_translator_names":["译者"],"mod_allow_direct_download":true,
                   "mod_releases":[{"attachment_id":42,"game_version_id":"098x","game_version":"0.98","mod_version":"1.0","file_size":5000000000,"download_url":"files?aid=42"}]},
                  {"mod_id":"reposted","mod_info_type":"reposted","mod_name_en":"Example","admin_notes":{"thread_comment":null}}
                ]
                """,
            "/api/meta/game_versions" => "[\"0.98\",\"0.97\"]",
            "/api/meta/mod_languages" => "{\"chinese\":\"中文\"}",
            "/api/meta/mod_categories" => "{\"library\":\"前置\"}",
            "/api/status" => "{\"status\":\"ok\"}",
            _ => throw new Exception("Unexpected URI " + path)
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }));
    var api = new ForumApiClient(http, new Uri("https://example.test/api"));
    var mods = await api.GetModsAsync();
    Check(mods.Count == 3 && mods[0].Releases == null && mods[1].Translators.Single() == "译者" && mods[2].EnglishName == "Example", "Union or nullable fields not parsed.");
    var release = mods[1].Releases!.Single();
    Check(release.FileSize == 5000000000, "File size must support Int64.");
    Check(api.GetDownloadUri(mods[1], release)!.AbsoluteUri == "https://example.test/api/files?aid=42", "Relative download URI did not preserve API prefix.");
    Check(api.GetDownloadUri(mods[0], release) == null, "Forbidden mod offered download.");
    release.DownloadUrl = "file:///C:/private.zip";
    Check(api.GetDownloadUri(mods[1], release) == null, "Non HTTP download accepted.");
    Check((await api.GetModsAsync(true)).Count == 3, "include_modding request incorrect.");
    Check((await api.GetGameVersionsAsync()).Count == 2, "Versions endpoint incorrect.");
    Check((await api.GetLanguagesAsync())["chinese"] == "中文", "Languages endpoint incorrect.");
    Check((await api.GetCategoriesAsync())["library"] == "前置", "Categories endpoint incorrect.");
    Check((await api.GetStatusAsync()).GetProperty("status").GetString() == "ok", "Status JSON unavailable.");

    using var failingHttp = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
    await MustThrow<HttpRequestException>(() => new ForumApiClient(failingHttp, new Uri("https://example.test")).GetModsAsync());
    using var canceled = new CancellationTokenSource();
    canceled.Cancel();
    await MustThrow<OperationCanceledException>(() => api.GetModsAsync(cancellationToken: canceled.Token));
}

static byte[] BuildZip()
{
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
    {
        using var writer = new StreamWriter(zip.CreateEntry("fixture/mod_info.json").Open());
        writer.Write("{\"id\":\"forum_fixture\",\"name\":\"论坛测试\",\"version\":\"1.0\"}");
    }
    return memory.ToArray();
}

static async Task CheckDownloads(string root)
{
    string cache = Path.Combine(root, "cache");
    var mod = new ForumMod { AllowDirectDownload = true };
    var release = new ForumModRelease { DownloadUrl = "https://files.test/forum.php?aid=42", FileName = "misleading.rar" };
    int requests = 0;
    byte[] bytes = BuildZip();
    using var http = new HttpClient(new FakeHandler(_ =>
    {
        requests++;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }));
    var api = new ForumApiClient(http, new Uri("https://example.test/"));
    var downloader = new ForumModDownloader(http, api, cache);
    string path;
    using (var archive = await downloader.DownloadAsync(mod, release))
    {
        path = archive.Path;
        Check(Path.GetExtension(path) == ".zip", "Format inferred from URL or filename instead of signature.");
        var result = ModInstaller.InstallArchive(path, Path.Combine(root, "pool"));
        Check(result.Success && result.ModId == "forum_fixture", "Downloaded ZIP did not install into isolated pool: " + result.Error);
    }
    Check(!File.Exists(path), "Installed archive was not cleaned.");
    mod.AllowDirectDownload = false;
    await MustThrow<InvalidOperationException>(() => downloader.DownloadAsync(mod, release));
    Check(requests == 1, "Forbidden download issued a request.");
    mod.AllowDirectDownload = true;
    foreach (var sample in new[]
    {
        (new byte[] {0x37,0x7A,0xBC,0xAF,0x27,0x1C,0,0}, ".7z"),
        (new byte[] {0x52,0x61,0x72,0x21,0x1A,7,0,0}, ".rar"),
        (new byte[] {0x52,0x61,0x72,0x21,0x1A,7,1,0}, ".rar")
    })
    {
        bytes = sample.Item1;
        using var archive = await downloader.DownloadAsync(mod, release);
        Check(Path.GetExtension(archive.Path) == sample.Item2, "RAR/7z signature incorrect.");
    }
    bytes = Encoding.UTF8.GetBytes("<html>please log in</html>");
    await MustThrow<InvalidDataException>(() => downloader.DownloadAsync(mod, release));
    Check(!Directory.EnumerateFiles(cache).Any(), "Invalid response left cache files.");

    using var htmlHttp = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("login", Encoding.UTF8, "text/html") }));
    await MustThrow<InvalidDataException>(() => new ForumModDownloader(htmlHttp, api, cache).DownloadAsync(mod, release));

    using var cancel = new CancellationTokenSource();
    using var cancelHttp = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new CancelStream(cancel)) }));
    await MustThrow<OperationCanceledException>(() => new ForumModDownloader(cancelHttp, api, cache).DownloadAsync(mod, release, cancellationToken: cancel.Token));
    Check(!Directory.EnumerateFiles(cache).Any(), "Canceled download left partial files.");

    using var delayedHeaders = new DelayedHeadersHandler();
    using var headersHttp = new HttpClient(delayedHeaders);
    var gameDownloader = new Sspcl.Core.Downloads.HttpPackageDownloader(headersHttp, cache);
    using (var canceledHeaders = new CancellationTokenSource())
    {
        var pending = gameDownloader.DownloadAsync(new Uri("https://files.test/game"), cancellationToken: canceledHeaders.Token, allowExecutable: true);
        canceledHeaders.Cancel();
        await MustThrow<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
        var lateStream = new MemoryStream(new byte[] { 0x4D, 0x5A, 0, 0, 0, 0, 0, 0 });
        delayedHeaders.Pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(lateStream) });
        await WaitUntil(() => !lateStream.CanRead);
    }
    bytes = new byte[] { 0x50, 0x4B, 3, 4, 0, 0, 0, 0 };
    using var retried = await downloader.DownloadAsync(mod, release);
    Check(File.Exists(retried.Path), "Download cannot restart after cancellation.");
}

static async Task WaitUntil(Func<bool> condition)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (!condition()) await Task.Delay(10, deadline.Token);
}

static async Task CheckParallelDownloads(string root)
{
    string cache = Path.Combine(root, "parallel");
    var mod = new ForumMod { Id = "parallel", AllowDirectDownload = true };
    var release = new ForumModRelease { AttachmentId = 1, DownloadUrl = "https://files.test/1" };
    using var stalledHttp = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new IgnoringCancellationStream()) }));
    var api = new ForumApiClient(stalledHttp, new Uri("https://forum.test/"));
    var stalledDownloader = new ForumModDownloader(stalledHttp, api, cache, TimeSpan.FromMilliseconds(100));
    await MustThrow<TimeoutException>(() => stalledDownloader.DownloadAsync(mod, release));
    using (var canceled = new CancellationTokenSource())
    {
        var downloading = new ForumModDownloader(stalledHttp, api, cache).DownloadAsync(mod, release, cancellationToken: canceled.Token);
        canceled.CancelAfter(50);
        await MustThrow<OperationCanceledException>(() => downloading.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    Check(Directory.GetFiles(cache).Length == 0, "Stall/cancel left partial files.");
    int active = 0, peak = 0, requests = 0;
    var unblock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var http = new HttpClient(new FakeHandler(_ =>
    {
        Interlocked.Increment(ref requests);
        int current = Interlocked.Increment(ref active);
        peak = Math.Max(peak, current);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(
            new GatedArchiveStream(BuildZip(), unblock.Task, () => Interlocked.Decrement(ref active))) };
    }));
    var queue = new ForumDownloadQueue(new ForumModDownloader(http, api, cache), 3);
    var releases = Enumerable.Range(1, 5).Select(id => new ForumModRelease { AttachmentId = id, DownloadUrl = "https://files.test/" + id }).ToArray();
    using var activeCancel = new CancellationTokenSource();
    using var queuedCancel = new CancellationTokenSource();
    var jobs = releases.Select((item, index) => queue.DownloadAsync(mod, item, cancellationToken:
        index == 0 ? activeCancel.Token : index == 4 ? queuedCancel.Token : CancellationToken.None)).ToArray();
    await WaitUntil(() => active == 3);
    Check(requests == 3, "Queue exceeded three concurrent transfers.");
    await MustThrow<InvalidOperationException>(() => queue.DownloadAsync(mod, releases[1]));
    queuedCancel.Cancel();
    await MustThrow<OperationCanceledException>(() => jobs[4].WaitAsync(TimeSpan.FromSeconds(2)));
    activeCancel.Cancel();
    await MustThrow<OperationCanceledException>(() => jobs[0].WaitAsync(TimeSpan.FromSeconds(2)));
    await WaitUntil(() => requests == 4);
    Check(peak <= 3, "Cancellation exceeded concurrency limit.");
    unblock.SetResult(true);
    foreach (var job in jobs.Skip(1).Take(3)) (await job).Dispose();
    using (var retry = await queue.DownloadAsync(mod, releases[0])) Check(File.Exists(retry.Path), "Canceled task could not be downloaded again.");
    Check(active == 0 && Directory.GetFiles(cache).Length == 0, "Parallel downloads leaked streams or temporary files.");
    Console.WriteLine("Parallel checks passed: stalled stream timeout, prompt cancellation, 3 transfers, queued/active cancel, duplicate prevention, freed slot and retry.");
}

static async Task CheckCatalogCache(string root)
{
    var now = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
    int requests = 0;
    bool fail = false;
    using var http = new HttpClient(new FakeHandler(request =>
    {
        requests++;
        if (fail) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var json = request.RequestUri!.AbsolutePath switch
        {
            "/mods" => "[{\"mod_id\":\"cached\",\"mod_name_cn\":\"缓存目录\"}]",
            "/meta/game_versions" => "[\"0.98\"]",
            "/meta/mod_categories" => "{\"utility\":\"工具\"}",
            "/meta/mod_languages" => "{\"chinese\":\"中文\"}",
            _ => throw new Exception("Unexpected cache endpoint")
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }));
    var api = new ForumApiClient(http, new Uri("https://cache.test/"));
    string directory = Path.Combine(root, "catalog");
    var repository = new ForumCatalogRepository(api, directory, () => now);
    Check(await repository.GetCachedAsync() == null && requests == 0, "Cache read sent HTTP.");
    var initial = await repository.RefreshAsync();
    Check(requests == 4 && !initial.IsStale && initial.Warning == null, "First load must fetch four endpoints.");
    var restarted = new ForumCatalogRepository(api, directory, () => now);
    Check((await restarted.GetCachedAsync())!.Mods.Data!.Single().Id == "cached", "Disk cache did not survive repository restart.");
    await restarted.RefreshAsync();
    Check(requests == 4, "Opening fresh disk cache sent HTTP.");
    await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => restarted.RefreshAsync(true)));
    Check(requests == 4, "Repeated refresh bypassed cooldown.");
    now += TimeSpan.FromMinutes(31);
    await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => restarted.RefreshAsync()));
    Check(requests == 5, "Expired index should fetch once; fresh metadata should not fetch.");
    now += TimeSpan.FromMinutes(31);
    fail = true;
    var offline = await restarted.RefreshAsync();
    Check(requests == 6 && offline.IsStale && offline.Warning != null && offline.Catalog.Mods.Data!.Count == 1,
        "Failed refresh lost offline catalog.");
    var offlineRestart = new ForumCatalogRepository(api, directory, () => now);
    await offlineRestart.RefreshAsync(true);
    Check(requests == 6, "Failure backoff was not persisted.");
    using (var canceled = new CancellationTokenSource())
    {
        canceled.Cancel();
        await MustThrow<OperationCanceledException>(() => offlineRestart.RefreshAsync(cancellationToken: canceled.Token));
        Check(requests == 6, "Canceled cache load sent HTTP.");
    }
    fail = false;
    now += TimeSpan.FromHours(25);
    var refreshed = await offlineRestart.RefreshAsync();
    Check(requests == 10 && refreshed.Warning == null && !refreshed.IsStale, "Old metadata did not refresh or failure state persisted.");
    Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Atomic write left temporary files.");
    await File.WriteAllTextAsync(Path.Combine(directory, "catalog-v1.json"), "{broken");
    var repaired = await new ForumCatalogRepository(api, directory, () => now).RefreshAsync();
    Check(requests == 14 && repaired.Catalog.Mods.Data!.Count == 1, "Corrupt cache did not recover.");
    var otherSource = new ForumApiClient(http, new Uri("https://another.test/"));
    Check(await new ForumCatalogRepository(otherSource, directory, () => now).GetCachedAsync() == null,
        "Cache leaked across API sources.");
    string blocked = Path.Combine(root, "not-a-directory");
    await File.WriteAllTextAsync(blocked, "fixture");
    var memoryOnly = await new ForumCatalogRepository(api, blocked, () => now).RefreshAsync();
    Check(memoryOnly.Catalog.Mods.Data!.Count == 1 && memoryOnly.Warning != null, "Disk write failure broke loaded catalog.");
    Console.WriteLine("Cache checks passed: restart, zero requests when fresh, independent expiry, refresh coalescing, offline fallback, persistent backoff, corruption, source isolation and write failure.");
}

static async Task CheckLive(string root, bool download)
{
    using var counter = new CountingHandler();
    using var http = new HttpClient(counter) { Timeout = TimeSpan.FromSeconds(60) };
    var api = new ForumApiClient(http, new Uri("https://api.fossic.org/"));
    string directory = Path.Combine(root, "live-catalog");
    var catalog = (await new ForumCatalogRepository(api, directory).RefreshAsync()).Catalog;
    var mods = catalog.Mods.Data!;
    int firstLoadRequests = counter.Requests;
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var reopened = new ForumCatalogRepository(api, directory);
    await reopened.GetCachedAsync();
    await reopened.RefreshAsync();
    clock.Stop();
    Check(firstLoadRequests == 4 && counter.Requests == firstLoadRequests, "Live cached reopen sent HTTP.");
    Console.WriteLine($"Live cache reopen: {clock.ElapsedMilliseconds} ms, 0 HTTP requests (first load: {firstLoadRequests}).");
    Check(mods.Count > 0 && (await api.GetStatusAsync()).GetProperty("status").GetString() == "ok", "Live API unavailable.");
    Console.WriteLine($"Live API: {mods.Count} mods, {catalog.Versions.Data!.Count} versions, {catalog.Categories.Data!.Count} categories, {catalog.Languages.Data!.Count} languages.");
    if (!download) return;
    var sample = mods.Where(m => m.AllowDirectDownload).SelectMany(m => (m.Releases ?? new()).Select(r => (Mod: m, Release: r)))
        .Where(x => x.Release.FileSize > 0 && x.Release.FileSize < 5 * 1024 * 1024 && x.Release.FileName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true && api.GetDownloadUri(x.Mod, x.Release) != null)
        .First(x => x.Mod.Id == "xsw_no_ff_mod");
    using var downloadHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    using var archive = await new ForumModDownloader(downloadHttp, api, Path.Combine(root, "live-cache")).DownloadAsync(sample.Mod, sample.Release);
    var installed = ModInstaller.InstallArchive(archive.Path, Path.Combine(root, "live-pool"));
    Check(installed.Success, "Live archive did not install: " + installed.Error);
    Console.WriteLine($"Live download and isolated install: {sample.Mod.ChineseName}, {new FileInfo(archive.Path).Length} bytes, mod_id={installed.ModId}.");
}

sealed class CountingHandler() : DelegatingHandler(new HttpClientHandler())
{
    private int _requests;
    public int Requests => _requests;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        return base.SendAsync(request, cancellationToken);
    }
}

sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Headers.UserAgent.ToString() != ForumClientIdentity.UserAgent ||
            !ForumClientIdentity.UserAgent.StartsWith("sspcl/"))
            throw new InvalidOperationException("Forum request must include the versioned SSPCL User-Agent.");
        return Task.FromResult(respond(request));
    }
}

sealed class DelayedHeadersHandler : HttpMessageHandler
{
    public TaskCompletionSource<HttpResponseMessage> Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Pending.Task;
}

sealed class CancelStream(CancellationTokenSource source) : MemoryStream(new byte[81920])
{
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        source.Cancel();
        return Task.FromCanceled<int>(cancellationToken);
    }
}

sealed class IgnoringCancellationStream : MemoryStream
{
    private readonly TaskCompletionSource<int> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _pending.Task;
    protected override void Dispose(bool disposing)
    {
        _pending.TrySetException(new ObjectDisposedException(nameof(IgnoringCancellationStream)));
        base.Dispose(disposing);
    }
}

sealed class GatedArchiveStream(byte[] archive, Task gate, Action closed) : MemoryStream(archive)
{
    private int _closed;
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        return await base.ReadAsync(buffer, offset, count, cancellationToken);
    }
    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0) closed();
        base.Dispose(disposing);
    }
}
