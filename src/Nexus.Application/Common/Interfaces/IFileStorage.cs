using Nexus.Domain.Common;

namespace Nexus.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<string> SaveFileAsync(Stream fileStream, string subDirectory, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetFileStreamAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default);
}

public interface IDocumentTextExtractor
{
    bool CanHandle(string extension, string contentType);
    Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default);
}

public interface ITextExtractor
{
    bool CanHandle(string contentType, string fileExtension);
    Task<string> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken cancellationToken = default);
}
