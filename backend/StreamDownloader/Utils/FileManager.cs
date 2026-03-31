using Serilog;

namespace StreamDownloader.Utils;

/// <summary>
/// 文件管理器实现
/// </summary>
public class FileManager : IFileManager
{
    private readonly string _tempBasePath;

    public FileManager()
    {
        _tempBasePath = Path.Combine(Path.GetTempPath(), "StreamDownloader");
        EnsureDirectory(_tempBasePath);
    }

    public void EnsureDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Log.Debug("Created directory: {Path}", path);
        }
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
                Log.Debug("Deleted directory: {Path}", path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete directory: {Path}", path);
            }
        }
    }

    public long GetFileSize(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path).Length;
        }
        return 0;
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string GetTempFilePath(string extension = ".tmp")
    {
        return Path.Combine(_tempBasePath, $"{Guid.NewGuid():N}{extension}");
    }

    public void CleanupTempFiles(TimeSpan maxAge)
    {
        if (!Directory.Exists(_tempBasePath)) return;

        var cutoff = DateTime.Now - maxAge;
        var directories = Directory.GetDirectories(_tempBasePath);

        foreach (var dir in directories)
        {
            try
            {
                var dirInfo = new DirectoryInfo(dir);
                if (dirInfo.CreationTime < cutoff)
                {
                    Directory.Delete(dir, true);
                    Log.Debug("Cleaned up old temp directory: {Path}", dir);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to cleanup temp directory: {Path}", dir);
            }
        }
    }
}
