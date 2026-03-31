using FluentAssertions;
using StreamDownloader.Utils;
using Xunit;

namespace StreamDownloader.Tests;

public class UrlHelperTests
{
    [Theory]
    [InlineData("https://example.com/video/stream.m3u8", "stream.m3u8")]
    [InlineData("https://example.com/live/index.m3u8?token=abc", "index.m3u8")]
    [InlineData("https://example.com/", "output")]
    [InlineData("https://example.com/path/to/video.ts", "video.ts")]
    public void GetFileNameFromUrl_ShouldExtractFileName(string url, string expected)
    {
        var result = UrlHelper.GetFileNameFromUrl(url);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com/video/stream.m3u8", "https://example.com/video/")]
    [InlineData("https://example.com/live/hls/index.m3u8", "https://example.com/live/hls/")]
    [InlineData("http://localhost:8080/path/stream.m3u8", "http://localhost:8080/path/")]
    public void GetBaseUrl_ShouldReturnBaseUrl(string url, string expected)
    {
        var result = UrlHelper.GetBaseUrl(url);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("segment001.ts", "https://example.com/video/", "https://example.com/video/segment001.ts")]
    [InlineData("/absolute/path.ts", "https://example.com/video/", "https://example.com/absolute/path.ts")]
    [InlineData("https://cdn.example.com/seg.ts", "https://example.com/video/", "https://cdn.example.com/seg.ts")]
    public void ResolveUrl_ShouldResolveCorrectly(string relativeUrl, string baseUrl, string expected)
    {
        var result = UrlHelper.ResolveUrl(relativeUrl, baseUrl);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com/live.m3u8", "HLS")]
    [InlineData("https://example.com/stream.flv", "FLV")]
    [InlineData("https://example.com/manifest.mpd", "DASH")]
    [InlineData("rtmp://example.com/live/stream", "RTMP")]
    [InlineData("https://example.com/video.mp4", "Unknown")]
    public void DetectProtocol_ShouldDetectCorrectly(string url, string expected)
    {
        var result = UrlHelper.DetectProtocol(url);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com/video.m3u8", true)]
    [InlineData("http://localhost:8080/stream", true)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not-a-url", false)]
    public void IsValidUrl_ShouldValidateCorrectly(string? url, bool expected)
    {
        var result = UrlHelper.IsValidUrl(url!);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("  https://example.com/video.m3u8  ", "https://example.com/video.m3u8")]
    [InlineData("https://example.com/\n video.m3u8", "https://example.com/video.m3u8")]
    public void CleanUrl_ShouldRemoveWhitespace(string url, string expected)
    {
        var result = UrlHelper.CleanUrl(url);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com/video.m3u8", ".ts", "video.ts")]
    [InlineData("https://example.com/stream.flv", ".flv", "stream.flv")]
    public void GenerateOutputFileName_ShouldGenerateCorrectly(string url, string ext, string expectedPrefix)
    {
        var result = UrlHelper.GenerateOutputFileName(url, ext);
        result.Should().StartWith(expectedPrefix.TrimEnd('_'));
    }

    [Fact]
    public void GenerateOutputFileName_WithEmptyPath_ShouldGenerateTimestampName()
    {
        var result = UrlHelper.GenerateOutputFileName("https://example.com/", ".ts");
        // 当路径为空时，返回 "output" 作为文件名
        result.Should().Be("output.ts");
    }
}
