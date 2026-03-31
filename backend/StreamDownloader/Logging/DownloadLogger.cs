using Serilog;
using StreamDownloader.Models;

namespace StreamDownloader.Logging;

/// <summary>
/// 下载操作专用日志记录器
/// </summary>
public static class DownloadLogger
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DownloadLogger));

    /// <summary>
    /// 记录下载开始
    /// </summary>
    public static void LogDownloadStart(string url, DownloadOptions options)
    {
        Logger.Information(
            "Download started: URL={Url}, Output={Output}, Threads={Threads}, Resume={Resume}",
            url, options.OutputPath, options.MaxThreads, options.EnableResume);
    }

    /// <summary>
    /// 记录流解析完成
    /// </summary>
    public static void LogStreamParsed(StreamInfo info)
    {
        Logger.Information(
            "Stream parsed: Protocol={Protocol}, Segments={SegmentCount}, Duration={Duration}, Live={IsLive}",
            info.Protocol, info.Segments.Count, info.Duration, info.IsLive);

        if (info.Encryption != null)
        {
            Logger.Information("Stream encryption: Method={Method}", info.Encryption.Method);
        }

        if (info.AvailableQualities.Count > 0)
        {
            Logger.Information("Available qualities: {Qualities}",
                string.Join(", ", info.AvailableQualities.Select(q => $"{q.Name}({q.Bandwidth}bps)")));
        }
    }

    /// <summary>
    /// 记录分片下载进度
    /// </summary>
    public static void LogSegmentProgress(int completed, int total, double speed)
    {
        var percentage = (double)completed / total * 100;
        var speedMbps = speed / 1024 / 1024;
        
        Logger.Debug(
            "Download progress: {Completed}/{Total} ({Percentage:F1}%), Speed={Speed:F2} MB/s",
            completed, total, percentage, speedMbps);
    }

    /// <summary>
    /// 记录分片下载成功
    /// </summary>
    public static void LogSegmentCompleted(int index, long size, TimeSpan elapsed)
    {
        Logger.Debug(
            "Segment {Index} completed: Size={Size} bytes, Time={Elapsed:F2}s",
            index, size, elapsed.TotalSeconds);
    }

    /// <summary>
    /// 记录分片下载失败
    /// </summary>
    public static void LogSegmentFailed(int index, Exception ex, int retryCount)
    {
        Logger.Warning(ex,
            "Segment {Index} failed (retry {RetryCount}): {Message}",
            index, retryCount, ex.Message);
    }

    /// <summary>
    /// 记录合并开始
    /// </summary>
    public static void LogMergeStart(int segmentCount, string outputPath)
    {
        Logger.Information(
            "Merging {SegmentCount} segments to {Output}",
            segmentCount, outputPath);
    }

    /// <summary>
    /// 记录下载完成
    /// </summary>
    public static void LogDownloadCompleted(string outputPath, long fileSize, TimeSpan totalTime)
    {
        var sizeMb = fileSize / 1024.0 / 1024.0;
        var avgSpeed = sizeMb / totalTime.TotalSeconds;

        Logger.Information(
            "Download completed: Output={Output}, Size={Size:F2} MB, Time={Time:F1}s, AvgSpeed={Speed:F2} MB/s",
            outputPath, sizeMb, totalTime.TotalSeconds, avgSpeed);
    }

    /// <summary>
    /// 记录下载失败
    /// </summary>
    public static void LogDownloadFailed(string url, Exception ex, TimeSpan elapsed)
    {
        Logger.Error(ex,
            "Download failed: URL={Url}, Time={Elapsed:F1}s, Error={Error}",
            url, elapsed.TotalSeconds, ex.Message);
    }

    /// <summary>
    /// 记录断点续传信息
    /// </summary>
    public static void LogResumeInfo(int completedSegments, int totalSegments)
    {
        Logger.Information(
            "Resuming download: {Completed}/{Total} segments already completed",
            completedSegments, totalSegments);
    }

    /// <summary>
    /// 记录HTTP请求
    /// </summary>
    public static void LogHttpRequest(string method, string url, int? statusCode = null)
    {
        if (statusCode.HasValue)
        {
            Logger.Debug("HTTP {Method} {Url} -> {StatusCode}", method, url, statusCode);
        }
        else
        {
            Logger.Debug("HTTP {Method} {Url}", method, url);
        }
    }

    /// <summary>
    /// 记录代理使用
    /// </summary>
    public static void LogProxyUsage(string proxyUrl)
    {
        Logger.Information("Using proxy: {Proxy}", proxyUrl);
    }

    /// <summary>
    /// 记录自定义请求头
    /// </summary>
    public static void LogCustomHeaders(Dictionary<string, string> headers)
    {
        if (headers.Count > 0)
        {
            Logger.Debug("Custom headers: {Headers}",
                string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}")));
        }
    }
}
