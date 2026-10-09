using System.Collections.Concurrent;

namespace Sspcl.Core.Store.Forum;

/// <summary>有界并行下载。取消独立于其他任务，所有结束路径归还并行槽。</summary>
public sealed class ForumDownloadQueue
{
    private readonly ForumModDownloader _downloader;
    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentDictionary<string, byte> _active = new();
    public ForumDownloadQueue(ForumModDownloader downloader, int concurrency = 3)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        if (concurrency < 1) throw new ArgumentOutOfRangeException(nameof(concurrency));
        _slots = new SemaphoreSlim(concurrency, concurrency);
    }

    public async Task<DownloadedModArchive> DownloadAsync(ForumMod mod, ForumModRelease release,
        IProgress<ModDownloadProgress>? progress = null, Action? started = null,
        CancellationToken cancellationToken = default)
    {
        var key = mod.Id + "|" + mod.InfoType + "|" + release.AttachmentId + "|" + release.DownloadUrl;
        if (!_active.TryAdd(key, 0)) throw new InvalidOperationException("该发布包已在下载队列中。");
        bool acquired = false;
        try
        {
            await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            cancellationToken.ThrowIfCancellationRequested();
            started?.Invoke();
            return await _downloader.DownloadAsync(mod, release, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (acquired) _slots.Release();
            _active.TryRemove(key, out _);
        }
    }
}
