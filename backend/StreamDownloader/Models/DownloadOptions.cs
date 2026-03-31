namespace StreamDownloader.Models;

/// <summary>
/// 下载配置选项
/// </summary>
public class DownloadOptions
{
    /// <summary>
    /// 输出文件路径
    /// </summary>
    public string OutputPath { get; set; } = "output.ts";

    /// <summary>
    /// 最大并发线程数
    /// </summary>
    public int MaxThreads { get; set; } = 4;

    /// <summary>
    /// 是否启用断点续传
    /// </summary>
    public bool EnableResume { get; set; } = true;

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 代理服务器地址
    /// </summary>
    public string? Proxy { get; set; }

    /// <summary>
    /// 自定义HTTP请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// 临时文件目录
    /// </summary>
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "StreamDownloader");

    /// <summary>
    /// 是否保留临时文件
    /// </summary>
    public bool KeepTempFiles { get; set; } = false;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（秒）
    /// </summary>
    public int RetryDelay { get; set; } = 2;

    /// <summary>
    /// 直播录制时长限制（0表示无限制，直到用户取消）
    /// </summary>
    public TimeSpan LiveDuration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 直播流播放列表轮询间隔（秒）
    /// </summary>
    public int LivePollInterval { get; set; } = 5;

    /// <summary>
    /// 直播流最大文件大小限制（字节，0表示无限制）
    /// </summary>
    public long MaxFileSize { get; set; } = 0;
}
