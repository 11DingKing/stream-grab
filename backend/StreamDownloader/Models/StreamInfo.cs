namespace StreamDownloader.Models;

/// <summary>
/// 流媒体信息
/// </summary>
public class StreamInfo
{
    /// <summary>
    /// 原始URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 基础URL（用于解析相对路径）
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 协议类型
    /// </summary>
    public StreamProtocol Protocol { get; set; }

    /// <summary>
    /// 分片列表
    /// </summary>
    public List<Segment> Segments { get; set; } = new();

    /// <summary>
    /// 总时长
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// 画质信息
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    /// 带宽
    /// </summary>
    public long? Bandwidth { get; set; }

    /// <summary>
    /// 加密信息
    /// </summary>
    public EncryptionInfo? Encryption { get; set; }

    /// <summary>
    /// 是否为直播流
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// 可用的画质列表（多码率流）
    /// </summary>
    public List<QualityOption> AvailableQualities { get; set; } = new();

    /// <summary>
    /// 媒体序列号（HLS直播流用于去重）
    /// </summary>
    public int MediaSequence { get; set; }

    /// <summary>
    /// 目标分片时长（秒，用于计算轮询间隔）
    /// </summary>
    public int TargetDuration { get; set; }
}

/// <summary>
/// 流媒体协议类型
/// </summary>
public enum StreamProtocol
{
    Unknown,
    Hls,      // m3u8
    Flv,      // HTTP-FLV
    Dash      // MPEG-DASH (预留)
}

/// <summary>
/// 分片信息
/// </summary>
public class Segment
{
    /// <summary>
    /// 分片索引
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 分片URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 分片时长
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// 分片大小（字节）
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// 加密密钥（如果有）
    /// </summary>
    public EncryptionInfo? Encryption { get; set; }

    /// <summary>
    /// 下载状态
    /// </summary>
    public SegmentStatus Status { get; set; } = SegmentStatus.Pending;

    /// <summary>
    /// 本地临时文件路径
    /// </summary>
    public string? LocalPath { get; set; }
}

/// <summary>
/// 分片下载状态
/// </summary>
public enum SegmentStatus
{
    Pending,
    Downloading,
    Completed,
    Failed
}

/// <summary>
/// 加密信息
/// </summary>
public class EncryptionInfo
{
    /// <summary>
    /// 加密方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 密钥URL
    /// </summary>
    public string? KeyUrl { get; set; }

    /// <summary>
    /// 密钥数据
    /// </summary>
    public byte[]? KeyData { get; set; }

    /// <summary>
    /// 初始化向量
    /// </summary>
    public byte[]? IV { get; set; }
}

/// <summary>
/// 画质选项
/// </summary>
public class QualityOption
{
    /// <summary>
    /// 画质名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 播放列表URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 带宽
    /// </summary>
    public long Bandwidth { get; set; }

    /// <summary>
    /// 分辨率
    /// </summary>
    public string? Resolution { get; set; }
}
