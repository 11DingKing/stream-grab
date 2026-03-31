using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;
using StreamDownloader.Logging;
using StreamDownloader.Models;
using StreamDownloader.Utils;

namespace StreamDownloader.Core;

/// <summary>
/// 流媒体下载服务实现
/// </summary>
public class StreamDownloaderService : IStreamDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEnumerable<IStreamHandler> _handlers;
    private readonly IFileManager _fileManager;

    public StreamDownloaderService(
        IHttpClientFactory httpClientFactory,
        IEnumerable<IStreamHandler> handlers,
        IFileManager fileManager)
    {
        _httpClientFactory = httpClientFactory;
        _handlers = handlers;
        _fileManager = fileManager;
    }

    public async Task DownloadAsync(string url, DownloadOptions options, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Log.Information("Starting download: {Url}", url);

        // 报告解析阶段
        progress?.Report(new DownloadProgress
        {
            Status = "Parsing stream...",
            Phase = DownloadPhase.Parsing
        });

        // 解析流信息
        var streamInfo = await ParseAsync(url, cancellationToken);
        var handler = GetHandler(url);

        if (handler == null)
        {
            throw new NotSupportedException($"Unsupported stream protocol for URL: {url}");
        }

        // 记录流解析结果
        DownloadLogger.LogStreamParsed(streamInfo);

        // 直播流自动进入录制模式（持续录制直到用户取消或达到限制）
        if (streamInfo.IsLive && handler.SupportsLiveRecording)
        {
            Log.Information("Detected live stream, using live recording mode (Ctrl+C to stop)");
            var httpClient = _httpClientFactory.CreateClient("default");
            _fileManager.EnsureDirectory(Path.GetDirectoryName(options.OutputPath)!);
            await handler.RecordLiveStreamAsync(url, options.OutputPath, httpClient, options, progress, cancellationToken);
            return;
        }

        // 创建临时目录
        var tempDir = Path.Combine(options.TempDirectory, Guid.NewGuid().ToString("N"));
        _fileManager.EnsureDirectory(tempDir);

        try
        {
            // 检查断点续传
            var downloadState = await LoadDownloadStateAsync(tempDir, options);
            var segmentsToDownload = streamInfo.Segments
                .Where(s => !downloadState.CompletedSegments.Contains(s.Index))
                .ToList();

            var totalSegments = streamInfo.Segments.Count;
            var completedCount = downloadState.CompletedSegments.Count;

            // 记录断点续传信息
            if (completedCount > 0)
            {
                DownloadLogger.LogResumeInfo(completedCount, totalSegments);
            }

            Log.Information("Segments to download: {Count} (already completed: {Completed})", 
                segmentsToDownload.Count, completedCount);

            // 并发下载分片
            var httpClient = _httpClientFactory.CreateClient("default");
            var semaphore = new SemaphoreSlim(options.MaxThreads);
            var downloadedSegments = new ConcurrentBag<int>();
            var failedSegments = new ConcurrentBag<(int Index, Exception Error)>();

            progress?.Report(new DownloadProgress
            {
                Status = "Downloading segments...",
                Phase = DownloadPhase.Downloading,
                TotalSegments = totalSegments,
                DownloadedSegments = completedCount
            });

            var downloadTasks = segmentsToDownload.Select(async segment =>
            {
                await semaphore.WaitAsync(cancellationToken);
                var segmentStopwatch = Stopwatch.StartNew();
                try
                {
                    var segmentPath = Path.Combine(tempDir, $"segment_{segment.Index:D6}.ts");
                    
                    // 检查是否已存在
                    if (File.Exists(segmentPath) && new FileInfo(segmentPath).Length > 0)
                    {
                        segment.LocalPath = segmentPath;
                        segment.Status = SegmentStatus.Completed;
                        downloadedSegments.Add(segment.Index);
                        return;
                    }

                    segment.Status = SegmentStatus.Downloading;
                    var data = await handler.DownloadSegmentAsync(segment, httpClient, options, cancellationToken);
                    
                    await File.WriteAllBytesAsync(segmentPath, data, cancellationToken);
                    segment.LocalPath = segmentPath;
                    segment.Status = SegmentStatus.Completed;
                    downloadedSegments.Add(segment.Index);

                    // 保存下载状态
                    await SaveDownloadStateAsync(tempDir, segment.Index);

                    segmentStopwatch.Stop();
                    DownloadLogger.LogSegmentCompleted(segment.Index, data.Length, segmentStopwatch.Elapsed);

                    var currentCompleted = completedCount + downloadedSegments.Count;
                    progress?.Report(new DownloadProgress
                    {
                        Status = "Downloading segments...",
                        Phase = DownloadPhase.Downloading,
                        TotalSegments = totalSegments,
                        DownloadedSegments = currentCompleted,
                        Percentage = (double)currentCompleted / totalSegments * 100
                    });

                    Log.Debug("Segment {Index} downloaded", segment.Index);
                }
                catch (Exception ex)
                {
                    segment.Status = SegmentStatus.Failed;
                    failedSegments.Add((segment.Index, ex));
                    DownloadLogger.LogSegmentFailed(segment.Index, ex, 0);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(downloadTasks);

            // 检查失败的分片
            if (failedSegments.Count > 0)
            {
                Log.Warning("{Count} segments failed to download", failedSegments.Count);
                
                // 重试失败的分片
                foreach (var (index, _) in failedSegments)
                {
                    var segment = streamInfo.Segments.First(s => s.Index == index);
                    for (int retry = 0; retry < options.RetryCount; retry++)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(options.RetryDelay), cancellationToken);
                            var segmentPath = Path.Combine(tempDir, $"segment_{segment.Index:D6}.ts");
                            var data = await handler.DownloadSegmentAsync(segment, httpClient, options, cancellationToken);
                            await File.WriteAllBytesAsync(segmentPath, data, cancellationToken);
                            segment.LocalPath = segmentPath;
                            segment.Status = SegmentStatus.Completed;
                            Log.Information("Segment {Index} downloaded on retry {Retry}", index, retry + 1);
                            break;
                        }
                        catch (Exception ex)
                        {
                            DownloadLogger.LogSegmentFailed(index, ex, retry + 1);
                        }
                    }
                }
            }

            // 合并分片
            progress?.Report(new DownloadProgress
            {
                Status = "Merging segments...",
                Phase = DownloadPhase.Merging,
                TotalSegments = totalSegments,
                DownloadedSegments = totalSegments,
                Percentage = 95
            });

            var segmentPaths = streamInfo.Segments
                .OrderBy(s => s.Index)
                .Select(s => s.LocalPath ?? Path.Combine(tempDir, $"segment_{s.Index:D6}.ts"))
                .Where(File.Exists)
                .ToList();

            DownloadLogger.LogMergeStart(segmentPaths.Count, options.OutputPath);

            _fileManager.EnsureDirectory(Path.GetDirectoryName(options.OutputPath)!);
            await handler.MergeSegmentsAsync(segmentPaths, options.OutputPath, cancellationToken);

            progress?.Report(new DownloadProgress
            {
                Status = "Completed!",
                Phase = DownloadPhase.Completed,
                TotalSegments = totalSegments,
                DownloadedSegments = totalSegments,
                Percentage = 100
            });

            Log.Information("Download completed: {Output}", options.OutputPath);
        }
        finally
        {
            // 清理临时文件
            if (!options.KeepTempFiles)
            {
                try
                {
                    Directory.Delete(tempDir, true);
                    Log.Debug("Temp directory cleaned: {TempDir}", tempDir);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to clean temp directory: {TempDir}", tempDir);
                }
            }
        }
    }

    public async Task<StreamInfo> ParseAsync(string url, CancellationToken cancellationToken = default)
    {
        var handler = GetHandler(url);
        if (handler == null)
        {
            throw new NotSupportedException($"Unsupported stream protocol for URL: {url}");
        }

        var httpClient = _httpClientFactory.CreateClient("default");
        return await handler.ParseAsync(url, httpClient, cancellationToken);
    }

    public IReadOnlyList<string> GetSupportedProtocols()
    {
        return _handlers.Select(h => h.ProtocolName).ToList();
    }

    private IStreamHandler? GetHandler(string url)
    {
        return _handlers.FirstOrDefault(h => h.CanHandle(url));
    }

    private async Task<DownloadState> LoadDownloadStateAsync(string tempDir, DownloadOptions options)
    {
        var statePath = Path.Combine(tempDir, "state.json");
        if (options.EnableResume && File.Exists(statePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(statePath);
                return System.Text.Json.JsonSerializer.Deserialize<DownloadState>(json) ?? new DownloadState();
            }
            catch
            {
                return new DownloadState();
            }
        }
        return new DownloadState();
    }

    private async Task SaveDownloadStateAsync(string tempDir, int segmentIndex)
    {
        var statePath = Path.Combine(tempDir, "state.json");
        var state = await LoadDownloadStateAsync(tempDir, new DownloadOptions { EnableResume = true });
        state.CompletedSegments.Add(segmentIndex);
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        await File.WriteAllTextAsync(statePath, json);
    }

    private class DownloadState
    {
        public HashSet<int> CompletedSegments { get; set; } = new();
    }
}
