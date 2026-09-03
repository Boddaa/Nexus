namespace Nexus.Application.DTOs.Search;

public record SearchRequest(
    string Query,
    int Page = 1,
    int PageSize = 20,
    string? Type = null
);

public record SearchResultDto(
    Guid Id,
    string Type,
    Guid WorkspaceId,
    Guid? PageId,
    string Title,
    string Snippet,
    double Score,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
