using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace StreamDownloader.Logging;

/// <summary>
/// 日志配置管理器
/// </summary>
public static class LoggingConfiguration
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" 
            ? "/app/logs" 
            : AppDomain.CurrentDomain.BaseDirectory,
        "logs");

    /// <summary>
    /// 配置默认日志
    /// </summary>
    public static void ConfigureDefault(bool verbose = false)
    {
        EnsureLogDirectory();

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "StreamDownloader")
            .Enrich.WithProperty("Version", GetVersion());

        // 控制台输出
        config.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Information);

        // 文件输出 - 普通日志
        config.WriteTo.File(
            path: Path.Combine(LogDirectory, "download-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}");

        // 文件输出 - 错误日志
        config.WriteTo.File(
            path: Path.Combine(LogDirectory, "error-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            restrictedToMinimumLevel: LogEventLevel.Error,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}");

        // JSON 格式日志（用于日志分析）
        if (verbose)
        {
            config.WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(LogDirectory, "download-json-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3);
        }

        Log.Logger = config.CreateLogger();
    }

    /// <summary>
    /// 配置详细日志模式
    /// </summary>
    public static void ConfigureVerbose()
    {
        ConfigureDefault(verbose: true);
    }

    /// <summary>
    /// 确保日志目录存在
    /// </summary>
    private static void EnsureLogDirectory()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    /// <summary>
    /// 获取应用版本
    /// </summary>
    private static string GetVersion()
    {
        return typeof(LoggingConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// 获取日志目录路径
    /// </summary>
    public static string GetLogDirectory() => LogDirectory;
}
