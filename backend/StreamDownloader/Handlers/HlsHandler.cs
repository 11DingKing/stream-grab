using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Serilog;
using StreamDownloader.Core;
using StreamDownloader.Models;
using StreamDownloader.Utils;

namespace StreamDownloader.Handlers;

/// <summary>
/// HLS (m3u8) 协议处理器 - 支持直播流轮询
/// </summary>
public class HlsHandler : IStreamHandler
{
    public string ProtocolName => "HLS";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".m3u8" };
    public bool SupportsLiveRecording => true;

    public bool CanHandle(string url)
    {
        var uri = new Uri(url);
        return uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("m3u8", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<StreamInfo> ParseAsync(string url, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        Log.Debug("Parsing HLS stream: {Url}", url);

        var content = await httpClient.GetStringAsync(url, cancellationToken);
        var baseUrl = GetBaseUrl(url);

        var streamInfo = new StreamInfo
        {
            Url = url,
            BaseUrl = baseUrl,
            Protocol = StreamProtocol.Hls
        };

        // 检查是否为Master Playlist
        if (content.Contains("#EXT-X-STREAM-INF"))
        {
            Log.Debug("Detected master playlist");
            streamInfo.AvailableQualities = ParseMasterPlaylist(content, baseUrl);
            
            // 选择最高画质
            var bestQuality = streamInfo.AvailableQualities
                .OrderByDescending(q => q.Bandwidth)
                .FirstOrDefault();

            if (bestQuality != null)
            {
                Log.Information("Selected quality: {Quality} ({Bandwidth} bps)", 
                    bestQuality.Resolution ?? "Unknown", bestQuality.Bandwidth);
                
                // 递归解析子播放列表
                return await ParseAsync(bestQuality.Url, httpClient, cancellationToken);
            }
        }

        // 解析Media Playlist
        streamInfo.Segments = ParseMediaPlaylist(content, baseUrl);
        streamInfo.Encryption = ParseEncryption(content, baseUrl);
        streamInfo.IsLive = !content.Contains("#EXT-X-ENDLIST");
        streamInfo.Duration = TimeSpan.FromSeconds(streamInfo.Segments.Sum(s => s.Duration));

        // 解析媒体序列号（用于直播流去重）
        var mediaSequenceMatch = Regex.Match(content, @"#EXT-X-MEDIA-SEQUENCE:(\d+)");
        if (mediaSequenceMatch.Success)
        {
            streamInfo.MediaSequence = int.Parse(mediaSequenceMatch.Groups[1].Value);
        }

        // 解析目标时长（用于计算轮询间隔）
        var targetDurationMatch = Regex.Match(content, @"#EXT-X-TARGETDURATION:(\d+)");
        if (targetDurationMatch.Success)
        {
            streamInfo.TargetDuration = int.Parse(targetDurationMatch.Groups[1].Value);
        }

        // 如果有加密，获取密钥
        if (streamInfo.Encryption != null && !string.IsNullOrEmpty(streamInfo.Encryption.KeyUrl))
        {
            Log.Debug("Fetching encryption key from: {KeyUrl}", streamInfo.Encryption.KeyUrl);
            streamInfo.Encryption.KeyData = await httpClient.GetByteArrayAsync(
                streamInfo.Encryption.KeyUrl, cancellationToken);
        }

        // 为每个分片设置加密信息
        foreach (var segment in streamInfo.Segments)
        {
            segment.Encryption = streamInfo.Encryption;
        }

        Log.Information("Parsed {Count} segments, Duration: {Duration}, Live: {IsLive}", 
            streamInfo.Segments.Count, streamInfo.Duration, streamInfo.IsLive);

        return streamInfo;
    }

    /// <summary>
    /// 录制HLS直播流 - 持续轮询播放列表获取新分片
    /// </summary>
    public async Task RecordLiveStreamAsync(string url, string outputPath, HttpClient httpClient, 
        DownloadOptions options, IProgress<DownloadProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        Log.Information("Starting HLS live stream recording: {Url}", url);

        var downloadedSegments = new HashSet<string>(); // 已下载的分片URL（去重）
        var segmentQueue = new ConcurrentQueue<Segment>(); // 待下载队列
        var startTime = DateTime.Now;
        long totalBytes = 0;
        int totalSegments = 0;

        // 创建输出文件流
        await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920);

        // 轮询间隔（默认使用目标时长的一半，最小1秒）
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.LivePollInterval));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 检查时长限制
                if (options.LiveDuration > TimeSpan.Zero && DateTime.Now - startTime >= options.LiveDuration)
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

                try
                {
                    // 解析播放列表获取新分片
                    var streamInfo = await ParseAsync(url, httpClient, cancellationToken);

                    // 动态调整轮询间隔
                    if (streamInfo.TargetDuration > 0)
                    {
                        pollInterval = TimeSpan.FromSeconds(Math.Max(1, streamInfo.TargetDuration / 2.0));
                    }

                    // 筛选新分片
                    var newSegments = streamInfo.Segments
                        .Where(s => !downloadedSegments.Contains(s.Url))
                        .ToList();

                    if (newSegments.Count > 0)
                    {
                        Log.Debug("Found {Count} new segments", newSegments.Count);

                        foreach (var segment in newSegments)
                        {
                            // 下载分片
                            try
                            {
                                var data = await DownloadSegmentAsync(segment, httpClient, options, cancellationToken);
                                await outputStream.WriteAsync(data, cancellationToken);
                                await outputStream.FlushAsync(cancellationToken);

                                downloadedSegments.Add(segment.Url);
                                totalBytes += data.Length;
                                totalSegments++;

                                var elapsed = DateTime.Now - startTime;
                                var speed = totalBytes / elapsed.TotalSeconds;

                                progress?.Report(new DownloadProgress
                                {
                                    Status = "Recording live stream...",
                                    Phase = DownloadPhase.Downloading,
                                    DownloadedSegments = totalSegments,
                                    DownloadedBytes = totalBytes,
                                    Speed = speed
                                });

                                Log.Debug("Segment downloaded: {Index}, Size: {Size} bytes", 
                                    segment.Index, data.Length);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to download segment: {Url}", segment.Url);
                            }
                        }
                    }

                    // 如果不是直播流，下载完成后退出
                    if (!streamInfo.IsLive)
                    {
                        Log.Information("VOD stream completed");
                        break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.Warning(ex, "Error polling playlist, will retry");
                }

                // 等待下一次轮询
                await Task.Delay(pollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Live recording cancelled by user");
        }

        var totalTime = DateTime.Now - startTime;
        Log.Information("HLS live recording completed: {Segments} segments, {Size} bytes, Duration: {Duration}",
            totalSegments, totalBytes, totalTime);

        progress?.Report(new DownloadProgress
        {
            Status = "Recording completed",
            Phase = DownloadPhase.Completed,
            DownloadedSegments = totalSegments,
            DownloadedBytes = totalBytes,
            Percentage = 100
        });
    }

    public async Task<byte[]> DownloadSegmentAsync(Segment segment, HttpClient httpClient, DownloadOptions options, CancellationToken cancellationToken = default)
    {
        byte[] data;

        if (options.SpeedLimit > 0)
        {
            using var response = await httpClient.GetAsync(segment.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var limiter = new SpeedLimiter(options.SpeedLimit);
            data = await limiter.ReadAllBytesWithLimitAsync(stream, cancellationToken);
        }
        else
        {
            data = await httpClient.GetByteArrayAsync(segment.Url, cancellationToken);
        }

        // 如果有加密，解密数据
        if (segment.Encryption != null && 
            segment.Encryption.Method == "AES-128" && 
            segment.Encryption.KeyData != null)
        {
            data = DecryptAes128(data, segment.Encryption.KeyData, segment.Encryption.IV, segment.Index);
        }

        return data;
    }

    public async Task MergeSegmentsAsync(IEnumerable<string> segmentPaths, string outputPath, CancellationToken cancellationToken = default)
    {
        Log.Debug("Merging {Count} segments to {Output}", segmentPaths.Count(), outputPath);

        await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);
        
        foreach (var segmentPath in segmentPaths)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            var data = await File.ReadAllBytesAsync(segmentPath, cancellationToken);
            await outputStream.WriteAsync(data, cancellationToken);
        }

        Log.Debug("Merge completed: {Size} bytes", outputStream.Length);
    }

    private List<QualityOption> ParseMasterPlaylist(string content, string baseUrl)
    {
        var qualities = new List<QualityOption>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("#EXT-X-STREAM-INF:"))
            {
                var quality = new QualityOption();

                // 解析带宽
                var bandwidthMatch = Regex.Match(line, @"BANDWIDTH=(\d+)");
                if (bandwidthMatch.Success)
                {
                    quality.Bandwidth = long.Parse(bandwidthMatch.Groups[1].Value);
                }

                // 解析分辨率
                var resolutionMatch = Regex.Match(line, @"RESOLUTION=(\d+x\d+)");
                if (resolutionMatch.Success)
                {
                    quality.Resolution = resolutionMatch.Groups[1].Value;
                }

                // 下一行是URL
                if (i + 1 < lines.Length)
                {
                    var urlLine = lines[i + 1].Trim();
                    if (!urlLine.StartsWith("#"))
                    {
                        quality.Url = ResolveUrl(urlLine, baseUrl);
                        quality.Name = quality.Resolution ?? $"{quality.Bandwidth / 1000}kbps";
                        qualities.Add(quality);
                    }
                }
            }
        }

        return qualities;
    }

    private List<Segment> ParseMediaPlaylist(string content, string baseUrl)
    {
        var segments = new List<Segment>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        double duration = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("#EXTINF:"))
            {
                // 解析时长
                var durationMatch = Regex.Match(trimmedLine, @"#EXTINF:([\d.]+)");
                if (durationMatch.Success)
                {
                    duration = double.Parse(durationMatch.Groups[1].Value);
                }
            }
            else if (!trimmedLine.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmedLine))
            {
                // 这是分片URL
                segments.Add(new Segment
                {
                    Index = index++,
                    Url = ResolveUrl(trimmedLine, baseUrl),
                    Duration = duration
                });
                duration = 0;
            }
        }

        return segments;
    }

    private EncryptionInfo? ParseEncryption(string content, string baseUrl)
    {
        var keyMatch = Regex.Match(content, @"#EXT-X-KEY:(.+)");
        if (!keyMatch.Success) return null;

        var keyLine = keyMatch.Groups[1].Value;
        var encryption = new EncryptionInfo();

        // 解析加密方法
        var methodMatch = Regex.Match(keyLine, @"METHOD=([^,]+)");
        if (methodMatch.Success)
        {
            encryption.Method = methodMatch.Groups[1].Value;
        }

        // 如果是NONE，返回null
        if (encryption.Method == "NONE") return null;

        // 解析密钥URL
        var uriMatch = Regex.Match(keyLine, @"URI=""([^""]+)""");
        if (uriMatch.Success)
        {
            encryption.KeyUrl = ResolveUrl(uriMatch.Groups[1].Value, baseUrl);
        }

        // 解析IV
        var ivMatch = Regex.Match(keyLine, @"IV=0x([0-9a-fA-F]+)");
        if (ivMatch.Success)
        {
            encryption.IV = Convert.FromHexString(ivMatch.Groups[1].Value);
        }

        return encryption;
    }

    private byte[] DecryptAes128(byte[] data, byte[] key, byte[]? iv, int segmentIndex)
    {
        // 如果没有IV，使用分片索引作为IV
        if (iv == null)
        {
            iv = new byte[16];
            BitConverter.GetBytes(segmentIndex).CopyTo(iv, 12);
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }

    private string GetBaseUrl(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash > 0)
        {
            path = path[..lastSlash];
        }
        return $"{uri.Scheme}://{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}{path}/";
    }

    private string ResolveUrl(string url, string baseUrl)
    {
        if (url.StartsWith("http://") || url.StartsWith("https://"))
        {
            return url;
        }
        
        if (url.StartsWith("/"))
        {
            var uri = new Uri(baseUrl);
            return $"{uri.Scheme}://{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}{url}";
        }

        return baseUrl + url;
    }
}
