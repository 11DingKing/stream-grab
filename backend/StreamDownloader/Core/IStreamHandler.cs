using StreamDownloader.Models;

namespace StreamDownloader.Core;

/// <summary>
/// 流媒体协议处理器接口
/// </summary>
public interface IStreamHandler
{
    /// <summary>
    /// 协议名称
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// 支持的文件扩展名
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// 判断是否支持该URL
    /// </summary>
    bool CanHandle(string url);

    /// <summary>
    /// 解析流媒体信息
    /// </summary>
    Task<StreamInfo> ParseAsync(string url, HttpClient httpClient, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载分片
    /// </summary>
    Task<byte[]> DownloadSegmentAsync(Segment segment, HttpClient httpClient, DownloadOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 合并分片
    /// </summary>
    Task MergeSegmentsAsync(IEnumerable<string> segmentPaths, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 录制直播流（持续下载直到取消或达到限制）
    /// </summary>
    Task RecordLiveStreamAsync(string url, string outputPath, HttpClient httpClient, DownloadOptions options,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否支持直播录制
    /// </summary>
    bool SupportsLiveRecording { get; }
}
