using Serilog;
using StreamDownloader.Core;
using StreamDownloader.Models;
using StreamDownloader.Utils;

namespace StreamDownloader.Handlers;

/// <summary>
/// FLV/HTTP-FLV 协议处理器
/// </summary>
public class FlvHandler : IStreamHandler
{
    public string ProtocolName => "FLV";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".flv" };
    public bool SupportsLiveRecording => true;

    private const int BufferSize = 81920; // 80KB buffer
    private const int FlvHeaderSize = 9;
    private static readonly byte[] FlvSignature = { 0x46, 0x4C, 0x56 }; // "FLV"

    public bool CanHandle(string url)
    {
        var uri = new Uri(url);
        return uri.AbsolutePath.EndsWith(".flv", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("flv", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("live", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<StreamInfo> ParseAsync(string url, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        Log.Debug("Parsing FLV stream: {Url}", url);

        // FLV流通常是直播流，我们创建一个单一的"分片"来表示整个流
        var streamInfo = new StreamInfo
        {
            Url = url,
            BaseUrl = url,
            Protocol = StreamProtocol.Flv,
            IsLive = true,
            Segments = new List<Segment>
            {
                new Segment
                {
                    Index = 0,
                    Url = url,
                    Duration = 0 // 直播流没有固定时长
                }
            }
        };

        // 尝试获取流信息
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            
            if (response.Content.Headers.ContentLength.HasValue)
            {
                streamInfo.Segments[0].Size = response.Content.Headers.ContentLength.Value;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not get FLV stream info via HEAD request");
        }

        return streamInfo;
    }

    public async Task<byte[]> DownloadSegmentAsync(Segment segment, HttpClient httpClient, DownloadOptions options, CancellationToken cancellationToken = default)
    {
        // 对于有明确大小的静态FLV文件，使用流式下载到临时文件避免内存溢出
        // 对于直播流，应使用 RecordLiveStreamAsync 方法
        
        using var response = await httpClient.GetAsync(segment.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        
        // 如果文件较大（>50MB），使用临时文件避免内存溢出
        if (contentLength.HasValue && contentLength.Value > 50 * 1024 * 1024)
        {
            return await DownloadLargeFileAsync(response, segment, options, cancellationToken);
        }

        // 小文件或未知大小的有限流，使用内存缓冲
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var inputStream = options.SpeedLimit > 0 
            ? new ThrottledStream(stream, options.SpeedLimit * 1024) 
            : stream;
        
        await using var inputStreamDisposable = inputStream;
        using var memoryStream = new MemoryStream();
        
        var buffer = new byte[BufferSize];
        int bytesRead;
        long totalBytes = 0;
        const long maxMemoryBytes = 50 * 1024 * 1024; // 内存缓冲上限50MB

        while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytes += bytesRead;

            // 如果是有限长度的文件，继续下载直到完成
            if (segment.Size.HasValue && totalBytes >= segment.Size.Value)
            {
                break;
            }

            // 防止无限流导致内存溢出（此情况应使用 RecordLiveStreamAsync）
            if (!segment.Size.HasValue && totalBytes >= maxMemoryBytes)
            {
                Log.Warning("Stream exceeds memory buffer limit, consider using live recording mode");
                break;
            }
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// 大文件流式下载到临时文件，避免内存溢出
    /// </summary>
    private async Task<byte[]> DownloadLargeFileAsync(HttpResponseMessage response, Segment segment, DownloadOptions options, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var inputStream = options.SpeedLimit > 0 
                ? new ThrottledStream(networkStream, options.SpeedLimit * 1024) 
                : networkStream;
            
            await using var inputStreamDisposable = inputStream;
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

            var buffer = new byte[BufferSize];
            int bytesRead;
            long totalBytes = 0;

            while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytes += bytesRead;

                if (segment.Size.HasValue && totalBytes >= segment.Size.Value)
                {
                    break;
                }
            }

            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();

            // 读取临时文件内容返回
            return await File.ReadAllBytesAsync(tempFile, cancellationToken);
        }
        finally
        {
            // 清理临时文件
            try { File.Delete(tempFile); } catch { }
        }
    }

    public async Task MergeSegmentsAsync(IEnumerable<string> segmentPaths, string outputPath, CancellationToken cancellationToken = default)
    {
        Log.Debug("Processing FLV segments to {Output}", outputPath);

        var paths = segmentPaths.ToList();
        if (paths.Count == 0)
        {
            throw new InvalidOperationException("No segments to merge");
        }

        // 对于单个FLV文件，直接复制
        if (paths.Count == 1)
        {
            File.Copy(paths[0], outputPath, true);
            return;
        }

        // 多个FLV文件需要合并
        await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
        bool headerWritten = false;

        foreach (var path in paths)
        {
            var data = await File.ReadAllBytesAsync(path, cancellationToken);
            
            if (!headerWritten)
            {
                // 写入第一个文件的完整内容（包括FLV头）
                await outputStream.WriteAsync(data, cancellationToken);
                headerWritten = true;
            }
            else
            {
                // 跳过后续文件的FLV头，只写入Tag数据
                if (data.Length > FlvHeaderSize + 4 && IsFlvHeader(data))
                {
                    // 跳过FLV头(9字节) + PreviousTagSize0(4字节)
                    await outputStream.WriteAsync(data.AsMemory(FlvHeaderSize + 4), cancellationToken);
                }
                else
                {
                    await outputStream.WriteAsync(data, cancellationToken);
                }
            }
        }

        Log.Debug("FLV merge completed: {Size} bytes", outputStream.Length);
    }

    /// <summary>
    /// 实时录制FLV直播流（实现IStreamHandler接口）
    /// </summary>
    public async Task RecordLiveStreamAsync(string url, string outputPath, HttpClient httpClient, 
        DownloadOptions options, IProgress<DownloadProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        Log.Information("Starting FLV live stream recording: {Url}", url);

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var inputStream = options.SpeedLimit > 0 
            ? new ThrottledStream(networkStream, options.SpeedLimit * 1024) 
            : networkStream;
        
        await using var inputStreamDisposable = inputStream;
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;
        long totalBytes = 0;
        var startTime = DateTime.Now;

        try
        {
            while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytes += bytesRead;

                var elapsed = DateTime.Now - startTime;
                var speed = elapsed.TotalSeconds > 0 ? totalBytes / elapsed.TotalSeconds : 0;

                // 检查时长限制
                if (options.LiveDuration > TimeSpan.Zero && elapsed >= options.LiveDuration)
                {
                    Log.Information("Live duration limit reached: {Duration}", options.LiveDuration);
                    break;
                }

                // 检查文件大小限制
                if (options.MaxFileSize > 0 && totalBytes >= options.MaxFileSize)
                {
                    Log.Information("Max file size limit reached: {Size} bytes", options.MaxFileSize);
                    break;
                }

                progress?.Report(new DownloadProgress
                {
                    Status = "Recording live stream...",
                    Phase = DownloadPhase.Downloading,
                    DownloadedBytes = totalBytes,
                    Speed = speed
                });

                // 定期刷新到磁盘
                if (totalBytes % (10 * 1024 * 1024) == 0) // 每10MB刷新一次
                {
                    await fileStream.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("FLV recording cancelled by user");
        }

        var totalTime = DateTime.Now - startTime;
        Log.Information("FLV recording completed: {Size} bytes, Duration: {Duration}", totalBytes, totalTime);

        progress?.Report(new DownloadProgress
        {
            Status = "Recording completed",
            Phase = DownloadPhase.Completed,
            DownloadedBytes = totalBytes,
            Percentage = 100
        });
    }

    private static bool IsFlvHeader(byte[] data)
    {
        if (data.Length < FlvHeaderSize) return false;
        return data[0] == FlvSignature[0] && 
               data[1] == FlvSignature[1] && 
               data[2] == FlvSignature[2];
    }
}
