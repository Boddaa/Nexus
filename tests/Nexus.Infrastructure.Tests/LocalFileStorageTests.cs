using System.Text;
using Microsoft.Extensions.Configuration;
using Nexus.Infrastructure.Storage;
using Xunit;

namespace Nexus.Infrastructure.Tests;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _testStorageDir;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), "Nexus_Storage_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testStorageDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DocumentsRoot"] = _testStorageDir
            })
            .Build();

        _storage = new LocalFileStorage(config);
    }

    [Fact]
    public async Task Normal_Relative_Path_Should_Succeed()
    {
        // Arrange
        var content = "Storage test payload";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var relativePath = await _storage.SaveFileAsync(stream, "test_folder", "payload.txt", "text/plain");

        // Assert
        Assert.NotNull(relativePath);
        Assert.True(await _storage.FileExistsAsync(relativePath));

        using var readStream = await _storage.GetFileStreamAsync(relativePath);
        using var reader = new StreamReader(readStream);
        var readContent = await reader.ReadToEndAsync();
        Assert.Equal(content, readContent);
    }

    [Fact]
    public async Task Nested_Valid_Path_Should_Succeed()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Nested content"));
        var relativePath = await _storage.SaveFileAsync(stream, "level1/level2/level3", "nested.txt", "text/plain");

        Assert.NotNull(relativePath);
        Assert.True(await _storage.FileExistsAsync(relativePath));
    }

    [Fact]
    public async Task DeleteFileAsync_Should_Remove_File()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("To be deleted"));
        var relativePath = await _storage.SaveFileAsync(stream, "to_delete.txt", "text/plain");

        // Act
        var deleted = await _storage.DeleteFileAsync(relativePath);

        // Assert
        Assert.True(deleted);
        Assert.False(await _storage.FileExistsAsync(relativePath));
    }

    [Fact]
    public void Path_Traversal_Slash_Should_Be_Rejected()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath("../../secret.txt"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath("folder/../../secret.txt"));
    }

    [Fact]
    public void Path_Traversal_Backslash_Should_Be_Rejected()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath(@"..\..\secret.txt"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath(@"folder\..\..\secret.txt"));
    }

    [Fact]
    public void Absolute_Windows_Path_Should_Be_Rejected()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath(@"C:\Windows\System32\cmd.exe"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath(@"D:\sensitive\data.txt"));
    }

    [Fact]
    public void Root_Escape_Attempt_Should_Be_Rejected()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath("/etc/passwd"));
    }

    [Fact]
    public async Task SaveFileAsync_With_Traversal_SubDirectory_Should_Throw()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("evil"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _storage.SaveFileAsync(stream, "../evil_dir", "hack.txt", "text/plain"));
    }

    [Fact]
    public async Task Operations_With_Traversal_Should_Throw()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _storage.GetFileStreamAsync("../../secret.txt"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _storage.DeleteFileAsync("../../secret.txt"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _storage.FileExistsAsync("../../secret.txt"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failure in temp directory
        }
    }
}
