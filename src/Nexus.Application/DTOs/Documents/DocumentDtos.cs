using Nexus.Domain.Enums;

namespace Nexus.Application.DTOs.Documents;

public record UploadDocumentStreamRequest(
    Stream Stream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? Title = null,
    Guid? PageId = null);

public record DocumentDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? PageId,
    string? PageTitle,
    string Title,
    string FileName,
    string ContentType,
    string FileExtension,
    long FileSizeBytes,
    DocumentStatus Status,
    string? ErrorMessage,
    int PageCount,
    int ExtractedTextLength,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record DocumentDetailDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? PageId,
    string? PageTitle,
    string Title,
    string FileName,
    string ContentType,
    string FileExtension,
    long FileSizeBytes,
    DocumentStatus Status,
    string? ErrorMessage,
    string? ExtractedText,
    int PageCount,
    int ExtractedTextLength,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record DocumentSummaryDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? PageId,
    string? PageTitle,
    string Title,
    string FileName,
    string ContentType,
    string FileExtension,
    long FileSizeBytes,
    DocumentStatus Status,
    DateTime CreatedAtUtc);

public record DocumentFileDownloadDto(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes);
