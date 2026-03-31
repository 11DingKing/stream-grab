using FluentAssertions;
using StreamDownloader.Models;
using Xunit;

namespace StreamDownloader.Tests;

public class DownloadProgressTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Act
        var progress = new DownloadProgress();

        // Assert
        progress.Status.Should().BeEmpty();
        progress.Percentage.Should().Be(0);
        progress.DownloadedSegments.Should().Be(0);
        progress.TotalSegments.Should().Be(0);
        progress.DownloadedBytes.Should().Be(0);
        progress.TotalBytes.Should().BeNull();
        progress.Speed.Should().Be(0);
        progress.EstimatedTimeRemaining.Should().BeNull();
        progress.Phase.Should().Be(DownloadPhase.Parsing);
    }

    [Fact]
    public void DownloadPhase_ShouldHaveCorrectValues()
    {
        // Assert
        DownloadPhase.Parsing.Should().Be((DownloadPhase)0);
        DownloadPhase.Downloading.Should().Be((DownloadPhase)1);
        DownloadPhase.Merging.Should().Be((DownloadPhase)2);
        DownloadPhase.Completed.Should().Be((DownloadPhase)3);
        DownloadPhase.Failed.Should().Be((DownloadPhase)4);
    }

    [Fact]
    public void Progress_ShouldCalculatePercentage()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            TotalSegments = 100,
            DownloadedSegments = 50
        };

        // Act
        progress.Percentage = (double)progress.DownloadedSegments / progress.TotalSegments * 100;

        // Assert
        progress.Percentage.Should().Be(50);
    }

    [Fact]
    public void Progress_ShouldTrackSpeed()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            DownloadedBytes = 10 * 1024 * 1024, // 10 MB
            Speed = 1024 * 1024 // 1 MB/s
        };

        // Assert
        progress.Speed.Should().Be(1024 * 1024);
    }

    [Fact]
    public void Progress_ShouldEstimateTimeRemaining()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            TotalBytes = 100 * 1024 * 1024, // 100 MB
            DownloadedBytes = 50 * 1024 * 1024, // 50 MB
            Speed = 10 * 1024 * 1024 // 10 MB/s
        };

        // Act - 剩余 50MB，速度 10MB/s，预计 5 秒
        if (progress.Speed > 0 && progress.TotalBytes.HasValue)
        {
            var remainingBytes = progress.TotalBytes.Value - progress.DownloadedBytes;
            progress.EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / progress.Speed);
        }

        // Assert
        progress.EstimatedTimeRemaining.Should().NotBeNull();
        progress.EstimatedTimeRemaining!.Value.TotalSeconds.Should().BeApproximately(5, 0.1);
    }
}
