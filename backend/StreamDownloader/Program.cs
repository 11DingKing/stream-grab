using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Serilog;
using Spectre.Console;
using StreamDownloader.Core;
using StreamDownloader.Handlers;
using StreamDownloader.Logging;
using StreamDownloader.Models;
using StreamDownloader.Utils;

namespace StreamDownloader;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // 配置默认日志
        LoggingConfiguration.ConfigureDefault();

        try
        {
            var rootCommand = BuildCommand();
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    static RootCommand BuildCommand()
    {
        var urlArgument = new Argument<string>("url", "The stream URL to download (m3u8/flv)");
        
        var outputOption = new Option<string>(
            aliases: new[] { "-o", "--output" },
            description: "Output file path",
            getDefaultValue: () => "output.ts");
        
        var threadsOption = new Option<int>(
            aliases: new[] { "-t", "--threads" },
            description: "Number of concurrent download threads",
            getDefaultValue: () => 4);
        
        var resumeOption = new Option<bool>(
            aliases: new[] { "-r", "--resume" },
            description: "Enable resume download",
            getDefaultValue: () => true);
        
        var timeoutOption = new Option<int>(
            aliases: new[] { "--timeout" },
            description: "Request timeout in seconds",
            getDefaultValue: () => 30);
        
        var proxyOption = new Option<string?>(
            aliases: new[] { "--proxy" },
            description: "Proxy server URL");
        
        var headerOption = new Option<string[]>(
            aliases: new[] { "--header", "-H" },
            description: "Custom HTTP headers (format: key:value)")
        { AllowMultipleArgumentsPerToken = true };
        
        var verboseOption = new Option<bool>(
            aliases: new[] { "-v", "--verbose" },
            description: "Enable verbose logging",
            getDefaultValue: () => false);

        var durationOption = new Option<int>(
            aliases: new[] { "-d", "--duration" },
            description: "Live stream recording duration in seconds (0 = unlimited)",
            getDefaultValue: () => 0);

        var maxSizeOption = new Option<long>(
            aliases: new[] { "--max-size" },
            description: "Maximum file size in MB (0 = unlimited)",
            getDefaultValue: () => 0);

        var pollIntervalOption = new Option<int>(
            aliases: new[] { "--poll-interval" },
            description: "Live stream playlist poll interval in seconds",
            getDefaultValue: () => 5);

        var rootCommand = new RootCommand("Stream Downloader - Download live streams from various protocols")
        {
            urlArgument,
            outputOption,
            threadsOption,
            resumeOption,
            timeoutOption,
            proxyOption,
            headerOption,
            verboseOption,
            durationOption,
            maxSizeOption,
            pollIntervalOption
        };

        rootCommand.SetHandler(async (context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var threads = context.ParseResult.GetValueForOption(threadsOption);
            var resume = context.ParseResult.GetValueForOption(resumeOption);
            var timeout = context.ParseResult.GetValueForOption(timeoutOption);
            var proxy = context.ParseResult.GetValueForOption(proxyOption);
            var headers = context.ParseResult.GetValueForOption(headerOption) ?? Array.Empty<string>();
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var duration = context.ParseResult.GetValueForOption(durationOption);
            var maxSize = context.ParseResult.GetValueForOption(maxSizeOption);
            var pollInterval = context.ParseResult.GetValueForOption(pollIntervalOption);

            // 重新配置日志（如果需要详细模式）
            if (verbose)
            {
                LoggingConfiguration.ConfigureVerbose();
            }

            var options = new DownloadOptions
            {
                OutputPath = output,
                MaxThreads = threads,
                EnableResume = resume,
                Timeout = TimeSpan.FromSeconds(timeout),
                Proxy = proxy,
                LiveDuration = TimeSpan.FromSeconds(duration),
                MaxFileSize = maxSize * 1024 * 1024, // Convert MB to bytes
                LivePollInterval = pollInterval
            };

            // 解析自定义headers
            foreach (var header in headers)
            {
                var parts = header.Split(':', 2);
                if (parts.Length == 2)
                {
                    options.Headers[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // 记录代理和自定义头
            if (!string.IsNullOrEmpty(proxy))
            {
                DownloadLogger.LogProxyUsage(proxy);
            }
            DownloadLogger.LogCustomHeaders(options.Headers);

            await ExecuteDownload(url, options);
        });

        return rootCommand;
    }

    static async Task ExecuteDownload(string url, DownloadOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        AnsiConsole.Write(new FigletText("Stream Downloader").Color(Color.Cyan1));
        AnsiConsole.MarkupLine($"[bold green]URL:[/] {url}");
        AnsiConsole.MarkupLine($"[bold green]Output:[/] {options.OutputPath}");
        AnsiConsole.MarkupLine($"[bold green]Threads:[/] {options.MaxThreads}");
        AnsiConsole.MarkupLine($"[bold green]Log Dir:[/] {LoggingConfiguration.GetLogDirectory()}");
        AnsiConsole.WriteLine();

        // 记录下载开始
        DownloadLogger.LogDownloadStart(url, options);

        var services = ConfigureServices(options);
        var downloader = services.GetRequiredService<IStreamDownloader>();

        try
        {
            // 检测是否为交互式终端，非交互式时使用文本进度输出
            var isInteractive = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("TERM") != null;

            if (isInteractive)
            {
                await AnsiConsole.Progress()
                    .AutoClear(false)
                    .HideCompleted(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("[cyan]Downloading...[/]", maxValue: 100);
                        
                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            task.Value = p.Percentage;
                            task.Description = $"[cyan]{p.Status}[/] ({p.DownloadedSegments}/{p.TotalSegments})";
                            
                            if (p.DownloadedSegments > 0 && p.DownloadedSegments % 10 == 0)
                            {
                                DownloadLogger.LogSegmentProgress(p.DownloadedSegments, p.TotalSegments, p.Speed);
                            }
                        });

                        await downloader.DownloadAsync(url, options, progress);
                        task.Value = 100;
                        task.Description = "[green]Completed![/]";
                    });
            }
            else
            {
                // 非交互式终端：使用纯文本进度输出
                var lastReportedSegments = -1;
                var progress = new Progress<DownloadProgress>(p =>
                {
                    // 每个分片完成都输出进度，让用户知道下载在进行中
                    if (p.DownloadedSegments != lastReportedSegments)
                    {
                        var speedStr = p.Speed > 0 ? $" @ {p.Speed / 1024.0 / 1024.0:F2} MB/s" : "";
                        Console.WriteLine($"[{p.Phase}] {p.Status} - {p.DownloadedSegments}/{p.TotalSegments} segments ({p.Percentage:F1}%){speedStr}");
                        Console.Out.Flush();
                        lastReportedSegments = p.DownloadedSegments;
                    }

                    if (p.DownloadedSegments > 0 && p.DownloadedSegments % 10 == 0)
                    {
                        DownloadLogger.LogSegmentProgress(p.DownloadedSegments, p.TotalSegments, p.Speed);
                    }
                });

                await downloader.DownloadAsync(url, options, progress);
            }

            stopwatch.Stop();
            
            // 获取文件大小
            var fileSize = new FileInfo(options.OutputPath).Length;
            
            Console.WriteLine($"✓ Download completed! Time: {stopwatch.Elapsed.TotalSeconds:F1}s, Size: {fileSize / 1024.0 / 1024.0:F2} MB");
            
            // 记录完成日志
            DownloadLogger.LogDownloadCompleted(options.OutputPath, fileSize, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            Console.WriteLine($"✗ Download failed: {ex.Message}");
            
            // 记录失败日志
            DownloadLogger.LogDownloadFailed(url, ex, stopwatch.Elapsed);
            throw;
        }
    }

    static IServiceProvider ConfigureServices(DownloadOptions options)
    {
        var services = new ServiceCollection();

        // 配置HttpClient
        services.AddHttpClient("default", client =>
        {
            // 设置较长的 HttpClient 超时，让 resilience handler 管理细粒度超时
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            foreach (var header in options.Headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(options.Proxy))
            {
                handler.Proxy = new System.Net.WebProxy(options.Proxy);
                handler.UseProxy = true;
            }
            return handler;
        })
        .AddStandardResilienceHandler(opts =>
        {
            // 放宽超时限制，避免大分片下载被中断
            opts.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            opts.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            // CircuitBreaker 采样时间必须 >= 2 * AttemptTimeout
            opts.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
        });

        // 注册服务
        services.AddSingleton(options);
        services.AddSingleton<IFileManager, FileManager>();
        services.AddSingleton<IStreamHandler, HlsHandler>();
        services.AddSingleton<IStreamHandler, FlvHandler>();
        services.AddSingleton<IStreamDownloader, StreamDownloaderService>();

        return services.BuildServiceProvider();
    }
}
