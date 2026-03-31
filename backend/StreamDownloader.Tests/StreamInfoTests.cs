using FluentAssertions;
using StreamDownloader.Models;
using Xunit;

namespace StreamDownloader.Tests;

public class StreamInfoTests
{
    [Fact]
    public void StreamInfo_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var info = new StreamInfo();

        // Assert
        info.Url.Should().BeEmpty();
        info.BaseUrl.Should().BeEmpty();
        info.Protocol.Should().Be(StreamProtocol.Unknown);
        info.Segments.Should().BeEmpty();
        info.Duration.Should().BeNull();
        info.Quality.Should().BeNull();
        info.Bandwidth.Should().BeNull();
        info.Encryption.Should().BeNull();
        info.IsLive.Should().BeFalse();
        info.AvailableQualities.Should().BeEmpty();
    }

    [Fact]
    public void Segment_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var segment = new Segment();

        // Assert
        segment.Index.Should().Be(0);
        segment.Url.Should().BeEmpty();
        segment.Duration.Should().Be(0);
        segment.Size.Should().BeNull();
        segment.Encryption.Should().BeNull();
        segment.Status.Should().Be(SegmentStatus.Pending);
        segment.LocalPath.Should().BeNull();
    }

    [Fact]
    public void SegmentStatus_ShouldHaveCorrectValues()
    {
        // Assert
        SegmentStatus.Pending.Should().Be((SegmentStatus)0);
        SegmentStatus.Downloading.Should().Be((SegmentStatus)1);
        SegmentStatus.Completed.Should().Be((SegmentStatus)2);
        SegmentStatus.Failed.Should().Be((SegmentStatus)3);
    }

    [Fact]
    public void StreamProtocol_ShouldHaveCorrectValues()
    {
        // Assert
        StreamProtocol.Unknown.Should().Be((StreamProtocol)0);
        StreamProtocol.Hls.Should().Be((StreamProtocol)1);
        StreamProtocol.Flv.Should().Be((StreamProtocol)2);
        StreamProtocol.Dash.Should().Be((StreamProtocol)3);
    }

    [Fact]
    public void EncryptionInfo_ShouldStoreValues()
    {
        // Arrange
        var encryption = new EncryptionInfo
        {
            Method = "AES-128",
            KeyUrl = "https://example.com/key",
            KeyData = new byte[] { 0x01, 0x02, 0x03 },
            IV = new byte[] { 0x00, 0x00, 0x00, 0x01 }
        };

        // Assert
        encryption.Method.Should().Be("AES-128");
        encryption.KeyUrl.Should().Be("https://example.com/key");
        encryption.KeyData.Should().HaveCount(3);
        encryption.IV.Should().HaveCount(4);
    }

    [Fact]
    public void QualityOption_ShouldStoreValues()
    {
        // Arrange
        var quality = new QualityOption
        {
            Name = "1080p",
            Url = "https://example.com/1080p.m3u8",
            Bandwidth = 5000000,
            Resolution = "1920x1080"
        };

        // Assert
        quality.Name.Should().Be("1080p");
        quality.Url.Should().Be("https://example.com/1080p.m3u8");
        quality.Bandwidth.Should().Be(5000000);
        quality.Resolution.Should().Be("1920x1080");
    }
}
