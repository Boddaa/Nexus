using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Document : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? PageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string? ExtractedText { get; set; }
    public string? Summary { get; set; }
    public int PageCount { get; set; } = 0;
    public string Checksum { get; set; } = string.Empty;

    public Workspace Workspace { get; set; } = null!;
    public Page? Page { get; set; }
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();

    public Document() { }
    public Document(Guid id) { Id = id; }
}

public class DocumentChunk : AuditableEntity
{
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public int StartCharOffset { get; set; }
    public int EndCharOffset { get; set; }
    public int PageNumber { get; set; } = 1;
    public string? VectorStoreId { get; set; }

    public Document Document { get; set; } = null!;
    public ICollection<SourceReference> SourceReferences { get; set; } = new List<SourceReference>();
}
