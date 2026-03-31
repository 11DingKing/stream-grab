using FluentAssertions;
using StreamDownloader.Models;
using Xunit;

namespace StreamDownloader.Tests;

public class DownloadOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Act
        var options = new DownloadOptions();

        // Assert
        options.OutputPath.Should().Be("output.ts");
        options.MaxThreads.Should().Be(4);
        options.EnableResume.Should().BeTrue();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.Proxy.Should().BeNull();
        options.Headers.Should().BeEmpty();
        options.KeepTempFiles.Should().BeFalse();
        options.RetryCount.Should().Be(3);
        options.RetryDelay.Should().Be(2);
    }

    [Fact]
    public void Headers_ShouldBeModifiable()
    {
        // Arrange
        var options = new DownloadOptions();

        // Act
        options.Headers["Referer"] = "https://example.com";
        options.Headers["Cookie"] = "session=abc123";

        // Assert
        options.Headers.Should().HaveCount(2);
        options.Headers["Referer"].Should().Be("https://example.com");
    }

    [Fact]
    public void TempDirectory_ShouldHaveDefaultValue()
    {
        // Act
        var options = new DownloadOptions();

        // Assert
        options.TempDirectory.Should().Contain("StreamDownloader");
        options.TempDirectory.Should().Contain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
    }
}
