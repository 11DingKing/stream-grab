using FluentAssertions;
using Moq;
using Moq.Protected;
using StreamDownloader.Handlers;
using StreamDownloader.Models;
using System.Net;
using Xunit;

namespace StreamDownloader.Tests;

public class FlvHandlerTests
{
    private readonly FlvHandler _handler;

    public FlvHandlerTests()
    {
        _handler = new FlvHandler();
    }

    [Theory]
    [InlineData("https://example.com/live.flv", true)]
    [InlineData("https://example.com/stream/video.flv", true)]
    [InlineData("https://example.com/live/stream", true)] // contains "live"
    [InlineData("https://example.com/video.mp4", false)]
    [InlineData("https://example.com/playlist.m3u8", false)]
    public void CanHandle_ShouldIdentifyFlvUrls(string url, bool expected)
    {
        var result = _handler.CanHandle(url);
        result.Should().Be(expected);
    }

    [Fact]
    public void ProtocolName_ShouldBeFLV()
    {
        _handler.ProtocolName.Should().Be("FLV");
    }

    [Fact]
    public void SupportedExtensions_ShouldContainFlv()
    {
        _handler.SupportedExtensions.Should().Contain(".flv");
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnStreamInfo()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(Array.Empty<byte>())
                {
                    Headers = { ContentLength = 1024 * 1024 }
                }
            });

        var httpClient = new HttpClient(mockHandler.Object);

        // Act
        var result = await _handler.ParseAsync("https://example.com/live.flv", httpClient);

        // Assert
        result.Should().NotBeNull();
        result.Protocol.Should().Be(StreamProtocol.Flv);
        result.IsLive.Should().BeTrue();
        result.Segments.Should().HaveCount(1);
        result.Segments[0].Url.Should().Be("https://example.com/live.flv");
    }

    [Fact]
    public async Task MergeSegmentsAsync_SingleFile_ShouldCopy()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "source.flv");
            var outputFile = Path.Combine(tempDir, "output.flv");
            var testData = new byte[] { 0x46, 0x4C, 0x56, 0x01, 0x05, 0x00, 0x00, 0x00, 0x09 }; // FLV header

            await File.WriteAllBytesAsync(sourceFile, testData);

            // Act
            await _handler.MergeSegmentsAsync(new[] { sourceFile }, outputFile);

            // Assert
            var result = await File.ReadAllBytesAsync(outputFile);
            result.Should().BeEquivalentTo(testData);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MergeSegmentsAsync_MultipleFiles_ShouldMergeWithoutDuplicateHeaders()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "part1.flv");
            var file2 = Path.Combine(tempDir, "part2.flv");
            var outputFile = Path.Combine(tempDir, "output.flv");

            // FLV header (9 bytes) + PreviousTagSize0 (4 bytes) + data
            var flvHeader = new byte[] { 0x46, 0x4C, 0x56, 0x01, 0x05, 0x00, 0x00, 0x00, 0x09 };
            var prevTagSize = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            var data1 = new byte[] { 0x01, 0x02, 0x03 };
            var data2 = new byte[] { 0x04, 0x05, 0x06 };

            await File.WriteAllBytesAsync(file1, flvHeader.Concat(prevTagSize).Concat(data1).ToArray());
            await File.WriteAllBytesAsync(file2, flvHeader.Concat(prevTagSize).Concat(data2).ToArray());

            // Act
            await _handler.MergeSegmentsAsync(new[] { file1, file2 }, outputFile);

            // Assert
            var result = await File.ReadAllBytesAsync(outputFile);
            // 第一个文件完整写入，第二个文件跳过 header + prevTagSize
            var expected = flvHeader.Concat(prevTagSize).Concat(data1).Concat(data2).ToArray();
            result.Should().BeEquivalentTo(expected);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MergeSegmentsAsync_EmptyList_ShouldThrow()
    {
        // Arrange
        var outputFile = Path.Combine(Path.GetTempPath(), "output.flv");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.MergeSegmentsAsync(Array.Empty<string>(), outputFile));
    }
}
