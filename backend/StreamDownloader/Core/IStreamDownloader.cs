using StreamDownloader.Models;

namespace StreamDownloader.Core;

/// <summary>
/// 流媒体下载器接口
/// </summary>
public interface IStreamDownloader
{
    /// <summary>
    /// 下载流媒体
    /// </summary>
    /// <param name="url">流媒体URL</param>
    /// <param name="options">下载选项</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DownloadAsync(string url, DownloadOptions options, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析流媒体信息
    /// </summary>
    /// <param name="url">流媒体URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<StreamInfo> ParseAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取支持的协议列表
    /// </summary>
    IReadOnlyList<string> GetSupportedProtocols();
}
