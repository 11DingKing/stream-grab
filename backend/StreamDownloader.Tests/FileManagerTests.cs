using FluentAssertions;
using StreamDownloader.Utils;
using Xunit;

namespace StreamDownloader.Tests;

public class FileManagerTests
{
    private readonly FileManager _fileManager;

    public FileManagerTests()
    {
        _fileManager = new FileManager();
    }

    [Fact]
    public void EnsureDirectory_ShouldCreateDirectory()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act
            _fileManager.EnsureDirectory(testDir);

            // Assert
            Directory.Exists(testDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void EnsureDirectory_ExistingDirectory_ShouldNotThrow()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);

        try
        {
            // Act & Assert
            var action = () => _fileManager.EnsureDirectory(testDir);
            action.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void EnsureDirectory_NullOrEmpty_ShouldNotThrow()
    {
        var action1 = () => _fileManager.EnsureDirectory(null!);
        var action2 = () => _fileManager.EnsureDirectory("");

        action1.Should().NotThrow();
        action2.Should().NotThrow();
    }

    [Fact]
    public void DeleteDirectory_ShouldDeleteDirectory()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "test.txt"), "test");

        // Act
        _fileManager.DeleteDirectory(testDir);

        // Assert
        Directory.Exists(testDir).Should().BeFalse();
    }

    [Fact]
    public void DeleteDirectory_NonExistent_ShouldNotThrow()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        var action = () => _fileManager.DeleteDirectory(testDir);
        action.Should().NotThrow();
    }

    [Fact]
    public void GetFileSize_ShouldReturnCorrectSize()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        var content = new byte[1024];
        File.WriteAllBytes(testFile, content);

        try
        {
            // Act
            var size = _fileManager.GetFileSize(testFile);

            // Assert
            size.Should().Be(1024);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void GetFileSize_NonExistent_ShouldReturnZero()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

        // Act
        var size = _fileManager.GetFileSize(testFile);

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public void FileExists_ShouldReturnCorrectResult()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        File.WriteAllText(testFile, "test");

        try
        {
            // Act & Assert
            _fileManager.FileExists(testFile).Should().BeTrue();
            _fileManager.FileExists(testFile + ".nonexistent").Should().BeFalse();
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void GetTempFilePath_ShouldReturnUniquePaths()
    {
        // Act
        var path1 = _fileManager.GetTempFilePath();
        var path2 = _fileManager.GetTempFilePath();
        var path3 = _fileManager.GetTempFilePath(".ts");

        // Assert
        path1.Should().NotBe(path2);
        path1.Should().EndWith(".tmp");
        path3.Should().EndWith(".ts");
    }
}
