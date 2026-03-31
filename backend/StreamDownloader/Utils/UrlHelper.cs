using System.Text.RegularExpressions;

namespace StreamDownloader.Utils;

/// <summary>
/// URL处理工具类
/// </summary>
public static class UrlHelper
{
    /// <summary>
    /// 从URL中提取文件名
    /// </summary>
    public static string GetFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var fileName = Path.GetFileName(path);
            
            // 移除查询参数
            var queryIndex = fileName.IndexOf('?');
            if (queryIndex > 0)
            {
                fileName = fileName[..queryIndex];
            }

            return string.IsNullOrEmpty(fileName) ? "output" : fileName;
        }
        catch
        {
            return "output";
        }
    }

    /// <summary>
    /// 获取URL的基础路径
    /// </summary>
    public static string GetBaseUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var lastSlash = path.LastIndexOf('/');
            
            if (lastSlash > 0)
            {
                path = path[..lastSlash];
            }

            var port = uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "";
            return $"{uri.Scheme}://{uri.Host}{port}{path}/";
        }
        catch
        {
            return url;
        }
    }

    /// <summary>
    /// 解析相对URL为绝对URL
    /// </summary>
    public static string ResolveUrl(string relativeUrl, string baseUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl))
            return baseUrl;

        // 已经是绝对URL
        if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://"))
            return relativeUrl;

        try
        {
            var baseUri = new Uri(baseUrl);

            // 以/开头的绝对路径
            if (relativeUrl.StartsWith("/"))
            {
                var port = baseUri.Port != 80 && baseUri.Port != 443 ? $":{baseUri.Port}" : "";
                return $"{baseUri.Scheme}://{baseUri.Host}{port}{relativeUrl}";
            }

            // 相对路径
            return new Uri(baseUri, relativeUrl).ToString();
        }
        catch
        {
            return baseUrl + relativeUrl;
        }
    }

    /// <summary>
    /// 检测URL的流媒体协议类型
    /// </summary>
    public static string DetectProtocol(string url)
    {
        var lowerUrl = url.ToLowerInvariant();

        if (lowerUrl.Contains(".m3u8") || lowerUrl.Contains("m3u8"))
            return "HLS";

        if (lowerUrl.Contains(".flv") || lowerUrl.Contains("flv"))
            return "FLV";

        if (lowerUrl.Contains(".mpd"))
            return "DASH";

        if (lowerUrl.StartsWith("rtmp://"))
            return "RTMP";

        return "Unknown";
    }

    /// <summary>
    /// 验证URL格式
    /// </summary>
    public static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// 清理URL（移除多余的空格和换行）
    /// </summary>
    public static string CleanUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        return Regex.Replace(url.Trim(), @"\s+", "");
    }

    /// <summary>
    /// 根据URL生成输出文件名
    /// </summary>
    public static string GenerateOutputFileName(string url, string defaultExtension = ".ts")
    {
        var fileName = GetFileNameFromUrl(url);
        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(extension) || extension == ".m3u8")
        {
            extension = defaultExtension;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(nameWithoutExt))
        {
            nameWithoutExt = $"download_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        return nameWithoutExt + extension;
    }
}
