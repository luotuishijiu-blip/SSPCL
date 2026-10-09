using System.Diagnostics;
using System.Net.Http;
using Sspcl.Core.Store.Forum;

namespace Sspcl.Core.Downloads;

/// <summary>通用包传输，支持取消、无数据超时及格式识别；游戏入口可显式允许 EXE，仅下载不执行。</summary>
public sealed class HttpPackageDownloader
{
    private readonly HttpClient _http;
    private readonly string _downloadDirectory;
    private readonly TimeSpan _idleTimeout;

    // 使用独立下载 HttpClient，避免将 API 认证头传给附件域名。
    public HttpPackageDownloader(HttpClient http, string? downloadDirectory = null, TimeSpan? idleTimeout = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _downloadDirectory = System.IO.Path.GetFullPath(downloadDirectory ?? System.IO.Path.GetTempPath());
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(60);
        if (_idleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleTimeout));
    }

    public async Task<DownloadedModArchive> DownloadAsync(Uri uri,
        IProgress<ModDownloadProgress>? progress = null, CancellationToken cancellationToken = default,
        bool allowExecutable = false)
    {
        if (uri == null || !uri.IsAbsoluteUri || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("下载地址必须为 HTTP(S)。", nameof(uri));
        Directory.CreateDirectory(_downloadDirectory);
        string partial = System.IO.Path.Combine(_downloadDirectory, "sspcl-forum-" + Guid.NewGuid().ToString("N") + ".partial");
        string? completed = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(ForumClientIdentity.UserAgent);
            using var response = await SendWithCancellationAsync(request, cancellationToken).ConfigureAwait(false);
            using var abort = cancellationToken.Register(() => response.Dispose());
            response.EnsureSuccessStatusCode();
            string mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                mediaType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidDataException("论坛返回了网页或错误消息，可能需要登录；请打开发布帖下载。");

            long? total = response.Content.Headers.ContentLength;
            long received = 0;
            var header = new byte[8];
            int headerCount = 0;
            var buffer = new byte[81920];
            var clock = Stopwatch.StartNew();
            progress?.Report(new ModDownloadProgress(0, total));
            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                buffer.Length, useAsync: true))
            {
                int read;
                while ((read = await ReadWithTimeoutAsync(source, buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    int copy = Math.Min(read, header.Length - headerCount);
                    if (copy > 0)
                    {
                        Array.Copy(buffer, 0, header, headerCount, copy);
                        headerCount += copy;
                    }
                    // 在前几个字节到齐后即拒绝非压缩包，避免把整页错误响应写入缓存。
                    if (headerCount == header.Length && GetArchiveExtension(header, headerCount, allowExecutable) == null)
                        throw new InvalidDataException("下载内容不是 ZIP、7z 或 RAR 压缩包，请打开发布帖检查。");
                    await target.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    received += read;
                    if (clock.ElapsedMilliseconds >= 200)
                    {
                        progress?.Report(new ModDownloadProgress(received, total));
                        clock.Restart();
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            string extension = GetArchiveExtension(header, headerCount, allowExecutable)
                ?? throw new InvalidDataException("下载内容为空或不是支持的压缩包。");
            if (total.HasValue && total.Value != received)
                throw new InvalidDataException("下载未完成，文件大小与服务器声明不一致。");
            completed = System.IO.Path.ChangeExtension(partial, extension);
            File.Move(partial, completed);
            progress?.Report(new ModDownloadProgress(received, total));
            return new DownloadedModArchive(completed);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            CleanFiles(partial, completed);
            throw new OperationCanceledException("下载已取消。", ex, cancellationToken);
        }
        catch
        {
            CleanFiles(partial, completed);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendWithCancellationAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var interrupted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => interrupted.TrySetResult(true));
        var sending = _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (await Task.WhenAny(sending, interrupted.Task).ConfigureAwait(false) != sending)
        {
            // 连接或响应头阶段也必须能退出；迟到的响应不能继续占用连接。
            _ = sending.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion) task.Result.Dispose();
                else if (task.IsFaulted) _ = task.Exception;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            token.ThrowIfCancellationRequested();
        }
        var response = await sending.ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            response.Dispose();
            token.ThrowIfCancellationRequested();
        }
        return response;
    }

    private async Task<int> ReadWithTimeoutAsync(Stream source, byte[] buffer, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_idleTimeout);
        var interrupted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = timeout.Token.Register(() => interrupted.TrySetResult(true));
        var reading = source.ReadAsync(buffer, 0, buffer.Length, timeout.Token);
        if (await Task.WhenAny(reading, interrupted.Task).ConfigureAwait(false) != reading)
        {
            // 部分 .NET Framework 网络流不响应 ReadAsync 的 CancellationToken。
            source.Dispose();
            _ = reading.ContinueWith(task => { _ = task.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("服务器已超过 " + _idleTimeout.TotalSeconds.ToString("0") + " 秒没有发送数据，请重试或打开发布帖。");
        }
        try { return await reading.ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && timeout.IsCancellationRequested)
        { throw new TimeoutException("下载连接长时间没有数据，请重试。"); }
    }

    private static void CleanFiles(string partial, string? completed)
    {
        try { File.Delete(partial); if (completed != null) File.Delete(completed); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string? GetArchiveExtension(byte[] header, int length, bool allowExecutable)
    {
        if (allowExecutable && length >= 2 && header[0] == 0x4D && header[1] == 0x5A) return ".exe";
        if (length >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
            ((header[2] == 3 && header[3] == 4) || (header[2] == 5 && header[3] == 6) || (header[2] == 7 && header[3] == 8))) return ".zip";
        if (length >= 7 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21 &&
            header[4] == 0x1A && header[5] == 7 &&
            (header[6] == 0 || (length >= 8 && header[6] == 1 && header[7] == 0))) return ".rar";
        if (length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC &&
            header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C) return ".7z";
        return null;
    }
}
