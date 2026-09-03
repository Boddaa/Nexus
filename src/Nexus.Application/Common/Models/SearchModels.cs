namespace Nexus.Application.Common.Models;

public record SearchRequest(
    Guid WorkspaceId,
    string Query,
    string? EntityTypeFilter = null, // "All", "Page", "Note", "Document", "Task", "Concept"
    int Limit = 20,
    bool UseSemanticSearch = true);

public record SearchResultItem(
    Guid EntityId,
    string EntityType,
    string Title,
    string Snippet,
    double Score,
    DateTime? UpdatedAtUtc);

public record SearchResponse(
    string Query,
    int TotalCount,
    IReadOnlyList<SearchResultItem> Items);
