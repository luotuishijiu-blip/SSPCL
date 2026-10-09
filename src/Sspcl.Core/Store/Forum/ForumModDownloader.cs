using System.Diagnostics;
using System.Net.Http;

namespace Sspcl.Core.Store.Forum;

public sealed class ModDownloadProgress
{
    public long ReceivedBytes { get; }
    public long? TotalBytes { get; }
    public double? Percent => TotalBytes > 0 ? ReceivedBytes * 100.0 / TotalBytes.Value : null;

    public ModDownloadProgress(long receivedBytes, long? totalBytes)
    {
        ReceivedBytes = receivedBytes;
        TotalBytes = totalBytes;
    }
}

/// <summary>下载结果由调用方释放；安装结束后清理临时压缩包。</summary>
public sealed class DownloadedModArchive : IDisposable
{
    public string Path { get; }
    internal DownloadedModArchive(string path) => Path = path;
    public void Dispose()
    {
        try { File.Delete(Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>论坛直下载许可检查，通用传输逻辑由 HttpPackageDownloader 提供。</summary>
public sealed class ForumModDownloader
{
    private readonly ForumApiClient _api;
    private readonly Sspcl.Core.Downloads.HttpPackageDownloader _downloader;
    public ForumModDownloader(HttpClient http, ForumApiClient api, string? downloadDirectory = null, TimeSpan? idleTimeout = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _downloader = new Sspcl.Core.Downloads.HttpPackageDownloader(http, downloadDirectory, idleTimeout);
    }
    public Task<DownloadedModArchive> DownloadAsync(ForumMod mod, ForumModRelease release,
        IProgress<ModDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var uri = _api.GetDownloadUri(mod, release)
            ?? throw new InvalidOperationException("该发布包不允许直接下载，请前往发布帖获取。");
        return _downloader.DownloadAsync(uri, progress, cancellationToken);
    }
}
