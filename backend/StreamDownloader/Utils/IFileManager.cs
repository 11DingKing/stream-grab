namespace StreamDownloader.Utils;

/// <summary>
/// 文件管理器接口
/// </summary>
public interface IFileManager
{
    /// <summary>
    /// 确保目录存在
    /// </summary>
    void EnsureDirectory(string path);

    /// <summary>
    /// 删除目录及其内容
    /// </summary>
    void DeleteDirectory(string path);

    /// <summary>
    /// 获取文件大小
    /// </summary>
    long GetFileSize(string path);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// 生成唯一的临时文件路径
    /// </summary>
    string GetTempFilePath(string extension = ".tmp");

    /// <summary>
    /// 清理过期的临时文件
    /// </summary>
    void CleanupTempFiles(TimeSpan maxAge);
}
