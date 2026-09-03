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
    public async Task SaveFileAsync_And_GetFileStreamAsync_Should_Work_Correctly()
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
    public void Path_Traversal_Attempt_Should_Throw_UnauthorizedAccessException()
    {
        // Relative escaping path
        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath("../../windows/system32/cmd.exe"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath(@"..\..\..\secret.txt"));

        // Direct slash attempts
        Assert.Throws<UnauthorizedAccessException>(() =>
            _storage.GetSafeFullPath("folder/../../../secret.txt"));
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
