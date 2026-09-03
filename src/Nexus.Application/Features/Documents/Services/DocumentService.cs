using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Documents;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Documents.Services;

public class DocumentService : IDocumentService
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
        ".md",
        ".txt"
    };

    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorage _fileStorage;
    private readonly IEnumerable<IDocumentTextExtractor> _extractors;

    public DocumentService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IFileStorage fileStorage,
        IEnumerable<IDocumentTextExtractor> extractors)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
        _extractors = extractors;
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return false;
        }

        var userId = _currentUserService.UserId.Value;
        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                (w.OwnerId == userId || w.Members.Any(m => m.UserId == userId && !m.IsDeleted)), cancellationToken);
    }

    public async Task<Result<DocumentDto>> UploadAsync(Guid workspaceId, UploadDocumentStreamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<DocumentDto>(Error.Unauthorized);
        }

        // 1. Validate File Empty
        if (request.Stream == null || request.FileSizeBytes <= 0)
        {
            return Result.Failure<DocumentDto>(new Error("Documents.EmptyFile", "Uploaded file cannot be empty."));
        }

        // 2. Validate File Size
        if (request.FileSizeBytes > MaxFileSizeBytes)
        {
            return Result.Failure<DocumentDto>(new Error("Documents.FileTooLarge", $"File size ({request.FileSizeBytes} bytes) exceeds the maximum allowed limit of 50 MB."));
        }

        // 3. Validate Extension
        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !SupportedExtensions.Contains(extension))
        {
            return Result.Failure<DocumentDto>(new Error("Documents.UnsupportedFormat", $"File format '{extension}' is not supported. Supported formats: .pdf, .docx, .md, .txt."));
        }

        // 4. Validate Page association if present
        string? pageTitle = null;
        if (request.PageId.HasValue)
        {
            var page = await _context.Pages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PageId.Value && p.WorkspaceId == workspaceId && !p.IsDeleted, cancellationToken);

            if (page == null)
            {
                return Result.Failure<DocumentDto>(new Error("Documents.PageNotFound", "The specified page does not exist in this workspace."));
            }

            pageTitle = page.Title;
        }

        var documentId = Guid.NewGuid();
        var storageSubDir = $"{workspaceId:N}/{documentId:N}";
        string? savedStoragePath = null;

        try
        {
            // 5. Store physical file
            savedStoragePath = await _fileStorage.SaveFileAsync(
                request.Stream,
                storageSubDir,
                request.FileName,
                request.ContentType,
                cancellationToken);

            // 6. Find appropriate text extractor
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(extension, request.ContentType));
            if (extractor == null)
            {
                await CleanupPhysicalFileAsync(savedStoragePath);
                return Result.Failure<DocumentDto>(new Error("Documents.NoExtractorFound", $"No text extractor available for '{extension}'."));
            }

            // 7. Extract text safely
            await using var extractionStream = await _fileStorage.GetFileStreamAsync(savedStoragePath, cancellationToken);
            var extractionResult = await extractor.ExtractTextAsync(extractionStream, cancellationToken);

            if (!extractionResult.IsSuccess)
            {
                // Cleanup on extraction failure
                await CleanupPhysicalFileAsync(savedStoragePath);
                return Result.Failure<DocumentDto>(extractionResult.Error);
            }

            var extractedText = extractionResult.Value;

            // 8. Create Document entity
            var document = new Document(documentId)
            {
                WorkspaceId = workspaceId,
                PageId = request.PageId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? request.FileName : request.Title.Trim(),
                FileName = Path.GetFileName(request.FileName),
                ContentType = request.ContentType,
                FileSizeBytes = request.FileSizeBytes,
                StoragePath = savedStoragePath,
                Status = DocumentStatus.Processed,
                ExtractedText = extractedText,
                PageCount = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                Checksum = string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            // 9. Persist to database
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(new DocumentDto(
                document.Id,
                document.WorkspaceId,
                document.PageId,
                pageTitle,
                document.Title,
                document.FileName,
                document.ContentType,
                extension.ToLowerInvariant(),
                document.FileSizeBytes,
                document.Status,
                document.ErrorMessage,
                document.PageCount,
                extractedText.Length,
                document.CreatedAtUtc,
                document.UpdatedAtUtc));
        }
        catch (Exception)
        {
            // Database or runtime failure: cleanup physical file to prevent orphans
            if (!string.IsNullOrEmpty(savedStoragePath))
            {
                await CleanupPhysicalFileAsync(savedStoragePath);
            }
            throw;
        }
    }

    public async Task<Result<IReadOnlyList<DocumentSummaryDto>>> GetDocumentsAsync(Guid workspaceId, Guid? pageId = null, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<DocumentSummaryDto>>(Error.Unauthorized);
        }

        var query = _context.Documents
            .AsNoTracking()
            .Include(d => d.Page)
            .Where(d => d.WorkspaceId == workspaceId && !d.IsDeleted);

        if (pageId.HasValue)
        {
            query = query.Where(d => d.PageId == pageId.Value);
        }

        var documents = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DocumentSummaryDto(
                d.Id,
                d.WorkspaceId,
                d.PageId,
                d.Page != null ? d.Page.Title : null,
                d.Title,
                d.FileName,
                d.ContentType,
                Path.GetExtension(d.FileName).ToLowerInvariant(),
                d.FileSizeBytes,
                d.Status,
                d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<DocumentSummaryDto>>(documents);
    }

    public async Task<Result<DocumentDetailDto>> GetDocumentByIdAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<DocumentDetailDto>(Error.Unauthorized);
        }

        var document = await _context.Documents
            .AsNoTracking()
            .Include(d => d.Page)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);

        if (document == null)
        {
            return Result.Failure<DocumentDetailDto>(Error.NotFound);
        }

        return Result.Success(new DocumentDetailDto(
            document.Id,
            document.WorkspaceId,
            document.PageId,
            document.Page != null ? document.Page.Title : null,
            document.Title,
            document.FileName,
            document.ContentType,
            Path.GetExtension(document.FileName).ToLowerInvariant(),
            document.FileSizeBytes,
            document.Status,
            document.ErrorMessage,
            document.ExtractedText,
            document.PageCount,
            document.ExtractedText?.Length ?? 0,
            document.CreatedAtUtc,
            document.UpdatedAtUtc));
    }

    public async Task<Result> DeleteAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(Error.Unauthorized);
        }

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);

        if (document == null)
        {
            return Result.Failure(Error.NotFound);
        }

        // Delete physical file (idempotent)
        await CleanupPhysicalFileAsync(document.StoragePath);

        // Soft delete metadata
        document.IsDeleted = true;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<DocumentFileDownloadDto>> GetFileStreamAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<DocumentFileDownloadDto>(Error.Unauthorized);
        }

        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.WorkspaceId == workspaceId && !d.IsDeleted, cancellationToken);

        if (document == null)
        {
            return Result.Failure<DocumentFileDownloadDto>(Error.NotFound);
        }

        if (!await _fileStorage.FileExistsAsync(document.StoragePath, cancellationToken))
        {
            return Result.Failure<DocumentFileDownloadDto>(Error.NotFound);
        }

        var fileStream = await _fileStorage.GetFileStreamAsync(document.StoragePath, cancellationToken);
        return Result.Success(new DocumentFileDownloadDto(fileStream, document.FileName, document.ContentType, document.FileSizeBytes));
    }

    private async Task CleanupPhysicalFileAsync(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return;

        try
        {
            await _fileStorage.DeleteFileAsync(storagePath, CancellationToken.None);
        }
        catch
        {
            // Do not fail main flow on physical delete exceptions
        }
    }
}
