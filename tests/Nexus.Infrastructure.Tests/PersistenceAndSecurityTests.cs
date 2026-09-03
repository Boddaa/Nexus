using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Security;
using Nexus.Infrastructure.Storage;
using Xunit;

namespace Nexus.Infrastructure.Tests;

public class PersistenceAndSecurityTests
{
    [Fact]
    public async Task AppDbContext_Should_Automatically_Set_CreatedAtUtc()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);

        var user = new User
        {
            Email = "audit@nexus.ai",
            FullName = "Audit Test"
        };

        // Act
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        Assert.NotEqual(default, user.CreatedAtUtc);
        Assert.Null(user.UpdatedAtUtc);

        // Act - Modify
        user.FullName = "Audit Test Modified";
        await context.SaveChangesAsync();

        // Assert
        Assert.NotNull(user.UpdatedAtUtc);
    }

    [Fact]
    public void BcryptPasswordHasher_Should_Hash_And_Verify_Correctly()
    {
        // Arrange
        var hasher = new BcryptPasswordHasher();
        var rawPassword = "P@ssw0rdEnterprise2026";

        // Act
        var hash = hasher.HashPassword(rawPassword);
        var isValid = hasher.VerifyPassword(rawPassword, hash);
        var isInvalid = hasher.VerifyPassword("WrongPassword", hash);

        // Assert
        Assert.NotEmpty(hash);
        Assert.NotEqual(rawPassword, hash);
        Assert.True(isValid);
        Assert.False(isInvalid);
    }

    [Fact]
    public async Task LocalFileStorage_Should_Save_Read_And_Delete_File()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "NexusStorageTests_" + Guid.NewGuid());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = tempDir
            })
            .Build();

        var storage = new LocalFileStorage(config);
        var fileContent = "NEXUS Knowledge File Storage Content Test";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

        // Act - Save
        var storagePath = await storage.SaveFileAsync(stream, "test_doc.txt", "text/plain");

        // Assert - Exists
        Assert.True(await storage.FileExistsAsync(storagePath));

        // Act - Read
        string content;
        using (var readStream = await storage.GetFileStreamAsync(storagePath))
        using (var reader = new StreamReader(readStream))
        {
            content = await reader.ReadToEndAsync();
        }
        Assert.Equal(fileContent, content);

        // Act - Delete
        var deleted = await storage.DeleteFileAsync(storagePath);
        Assert.True(deleted);
        Assert.False(await storage.FileExistsAsync(storagePath));

        // Cleanup
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }
}
