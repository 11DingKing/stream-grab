namespace StreamDownloader.Models;

/// <summary>
/// 下载进度信息
/// </summary>
public class DownloadProgress
{
    /// <summary>
    /// 当前状态描述
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 完成百分比 (0-100)
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// 已下载分片数
    /// </summary>
    public int DownloadedSegments { get; set; }

    /// <summary>
    /// 总分片数
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 已下载字节数
    /// </summary>
    public long DownloadedBytes { get; set; }

    /// <summary>
    /// 总字节数（如果已知）
    /// </summary>
    public long? TotalBytes { get; set; }

    /// <summary>
    /// 当前下载速度（字节/秒）
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    /// 预计剩余时间
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// 当前阶段
    /// </summary>
    public DownloadPhase Phase { get; set; }
}

/// <summary>
/// 下载阶段
/// </summary>
public enum DownloadPhase
{
    Parsing,      // 解析播放列表
    Downloading,  // 下载分片
    Merging,      // 合并文件
    Completed,    // 完成
    Failed        // 失败
}
