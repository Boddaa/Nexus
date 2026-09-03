using Microsoft.Extensions.Configuration;
using Nexus.Application.Common.Interfaces;

namespace Nexus.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _baseStoragePath;

    public LocalFileStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:DocumentsRoot"]
            ?? configuration["FileStorage:BasePath"]
            ?? configuration["Storage:DocumentsPath"];

        _baseStoragePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus", "Storage")
            : Path.GetFullPath(configuredPath);

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        return await SaveFileAsync(fileStream, DateTime.UtcNow.ToString("yyyyMM"), fileName, contentType, cancellationToken);
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string subDirectory, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (!string.IsNullOrWhiteSpace(subDirectory))
        {
            if (subDirectory.Contains("..") ||
                subDirectory.Contains(':') ||
                Path.IsPathRooted(subDirectory))
            {
                throw new UnauthorizedAccessException($"Path traversal attempt detected in subDirectory: '{subDirectory}'.");
            }
        }

        var safeExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}{safeExtension}";

        var cleanSubDir = (subDirectory ?? string.Empty).Trim('/', '\\');
        var relativePath = Path.Combine(cleanSubDir, uniqueFileName);
        var fullPath = GetSafeFullPath(relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var destinationStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(destinationStream, cancellationToken);

        // Return relative path with forward slashes for storage consistency
        return relativePath.Replace('\\', '/');
    }

    public Task<Stream> GetFileStreamAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetSafeFullPath(storagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Stored file not found at: {storagePath}");
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetSafeFullPath(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetSafeFullPath(storagePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public string GetSafeFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Storage path cannot be empty.", nameof(relativePath));
        }

        // 1. Explicitly reject traversal tokens, drive specs, or rooted paths
        if (relativePath.Contains("..") ||
            relativePath.Contains(':') ||
            Path.IsPathRooted(relativePath))
        {
            throw new UnauthorizedAccessException($"Path traversal attempt detected: '{relativePath}' is rooted or contains traversal operators.");
        }

        // 2. Normalize directory separators
        var normalizedRelative = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        // 3. Resolve absolute canonical path
        var fullPath = Path.GetFullPath(Path.Combine(_baseStoragePath, normalizedRelative));

        // 4. Verify resolved path stays strictly inside configured base storage root
        var baseWithSeparator = _baseStoragePath.EndsWith(Path.DirectorySeparatorChar)
            ? _baseStoragePath
            : _baseStoragePath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _baseStoragePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Path traversal attempt detected: '{relativePath}' escapes base storage.");
        }

        return fullPath;
    }
}
