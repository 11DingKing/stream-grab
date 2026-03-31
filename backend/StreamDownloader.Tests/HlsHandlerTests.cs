using FluentAssertions;
using Moq;
using Moq.Protected;
using StreamDownloader.Handlers;
using StreamDownloader.Models;
using System.Net;
using Xunit;

namespace StreamDownloader.Tests;

public class HlsHandlerTests
{
    private readonly HlsHandler _handler;

    public HlsHandlerTests()
    {
        _handler = new HlsHandler();
    }

    [Theory]
    [InlineData("https://example.com/live.m3u8", true)]
    [InlineData("https://example.com/stream/index.m3u8", true)]
    [InlineData("https://example.com/hls/playlist.m3u8?token=abc", true)]
    [InlineData("https://example.com/video.mp4", false)]
    [InlineData("https://example.com/stream.flv", false)]
    public void CanHandle_ShouldIdentifyHlsUrls(string url, bool expected)
    {
        var result = _handler.CanHandle(url);
        result.Should().Be(expected);
    }

    [Fact]
    public void ProtocolName_ShouldBeHLS()
    {
        _handler.ProtocolName.Should().Be("HLS");
    }

    [Fact]
    public void SupportedExtensions_ShouldContainM3u8()
    {
        _handler.SupportedExtensions.Should().Contain(".m3u8");
    }

    [Fact]
    public async Task ParseAsync_ShouldParseSimplePlaylist()
    {
        // Arrange
        var m3u8Content = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:10
#EXTINF:10.0,
segment001.ts
#EXTINF:10.0,
segment002.ts
#EXTINF:10.0,
segment003.ts
#EXT-X-ENDLIST";

        var httpClient = CreateMockHttpClient(m3u8Content);

        // Act
        var result = await _handler.ParseAsync("https://example.com/video/playlist.m3u8", httpClient);

        // Assert
        result.Should().NotBeNull();
        result.Protocol.Should().Be(StreamProtocol.Hls);
        result.Segments.Should().HaveCount(3);
        result.IsLive.Should().BeFalse();
        result.Segments[0].Url.Should().Be("https://example.com/video/segment001.ts");
        result.Segments[1].Url.Should().Be("https://example.com/video/segment002.ts");
        result.Segments[2].Url.Should().Be("https://example.com/video/segment003.ts");
    }

    [Fact]
    public async Task ParseAsync_ShouldDetectLiveStream()
    {
        // Arrange - 没有 #EXT-X-ENDLIST 表示直播流
        var m3u8Content = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:10
#EXTINF:10.0,
segment001.ts
#EXTINF:10.0,
segment002.ts";

        var httpClient = CreateMockHttpClient(m3u8Content);

        // Act
        var result = await _handler.ParseAsync("https://example.com/live/playlist.m3u8", httpClient);

        // Assert
        result.IsLive.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ShouldCalculateDuration()
    {
        // Arrange
        var m3u8Content = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:10
#EXTINF:9.5,
segment001.ts
#EXTINF:10.0,
segment002.ts
#EXTINF:8.5,
segment003.ts
#EXT-X-ENDLIST";

        var httpClient = CreateMockHttpClient(m3u8Content);

        // Act
        var result = await _handler.ParseAsync("https://example.com/video/playlist.m3u8", httpClient);

        // Assert
        result.Duration.Should().NotBeNull();
        result.Duration!.Value.TotalSeconds.Should().BeApproximately(28.0, 0.1);
    }

    [Fact]
    public async Task ParseAsync_ShouldHandleAbsoluteUrls()
    {
        // Arrange
        var m3u8Content = @"#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:10
#EXTINF:10.0,
https://cdn.example.com/segment001.ts
#EXTINF:10.0,
https://cdn.example.com/segment002.ts
#EXT-X-ENDLIST";

        var httpClient = CreateMockHttpClient(m3u8Content);

        // Act
        var result = await _handler.ParseAsync("https://example.com/video/playlist.m3u8", httpClient);

        // Assert
        result.Segments[0].Url.Should().Be("https://cdn.example.com/segment001.ts");
        result.Segments[1].Url.Should().Be("https://cdn.example.com/segment002.ts");
    }

    [Fact]
    public async Task DownloadSegmentAsync_ShouldDownloadData()
    {
        // Arrange
        var segmentData = new byte[] { 0x47, 0x40, 0x00, 0x10 }; // TS packet header
        var httpClient = CreateMockHttpClient(segmentData);
        var segment = new Segment
        {
            Index = 0,
            Url = "https://example.com/segment001.ts"
        };

        // Act
        var result = await _handler.DownloadSegmentAsync(segment, httpClient);

        // Assert
        result.Should().BeEquivalentTo(segmentData);
    }

    [Fact]
    public async Task MergeSegmentsAsync_ShouldMergeFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var segment1 = Path.Combine(tempDir, "seg1.ts");
            var segment2 = Path.Combine(tempDir, "seg2.ts");
            var output = Path.Combine(tempDir, "output.ts");

            await File.WriteAllBytesAsync(segment1, new byte[] { 0x01, 0x02, 0x03 });
            await File.WriteAllBytesAsync(segment2, new byte[] { 0x04, 0x05, 0x06 });

            // Act
            await _handler.MergeSegmentsAsync(new[] { segment1, segment2 }, output);

            // Assert
            var result = await File.ReadAllBytesAsync(output);
            result.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static HttpClient CreateMockHttpClient(string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });

        return new HttpClient(mockHandler.Object);
    }

    private static HttpClient CreateMockHttpClient(byte[] content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(content)
            });

        return new HttpClient(mockHandler.Object);
    }
}
