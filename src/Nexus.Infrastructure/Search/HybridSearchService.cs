using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;

namespace Nexus.Infrastructure.Search;

public class HybridSearchService : IHybridSearchService
{
    private readonly IAppDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public HybridSearchService(
        IAppDbContext context,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore)
    {
        _context = context;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task<SearchResponse> HybridSearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResultItem>();
        var query = request.Query.Trim().ToLowerInvariant();

        // Keyword search on Pages
        if (string.IsNullOrEmpty(request.EntityTypeFilter) || request.EntityTypeFilter == "All" || request.EntityTypeFilter == "Page")
        {
            var pages = await _context.Pages
                .AsNoTracking()
                .Where(p => p.WorkspaceId == request.WorkspaceId && !p.IsDeleted &&
                            (p.Title.ToLower().Contains(query) || p.ContentJson.ToLower().Contains(query)))
                .Take(10)
                .Select(p => new SearchResultItem(p.Id, "Page", p.Title, "Page content matching search query", 0.85, p.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(pages);
        }

        // Keyword search on Notes
        if (string.IsNullOrEmpty(request.EntityTypeFilter) || request.EntityTypeFilter == "All" || request.EntityTypeFilter == "Note")
        {
            var notes = await _context.Notes
                .AsNoTracking()
                .Where(n => n.WorkspaceId == request.WorkspaceId && !n.IsDeleted &&
                            (n.Title.ToLower().Contains(query) || n.Content.ToLower().Contains(query)))
                .Take(10)
                .Select(n => new SearchResultItem(n.Id, "Note", n.Title, n.Content.Length > 100 ? n.Content.Substring(0, 100) + "..." : n.Content, 0.88, n.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(notes);
        }

        // Keyword search on Documents
        if (string.IsNullOrEmpty(request.EntityTypeFilter) || request.EntityTypeFilter == "All" || request.EntityTypeFilter == "Document")
        {
            var docs = await _context.Documents
                .AsNoTracking()
                .Where(d => d.WorkspaceId == request.WorkspaceId && !d.IsDeleted &&
                            (d.Title.ToLower().Contains(query) || (d.ExtractedText != null && d.ExtractedText.ToLower().Contains(query))))
                .Take(10)
                .Select(d => new SearchResultItem(d.Id, "Document", d.Title, d.Summary ?? d.FileName, 0.90, d.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(docs);
        }

        // Semantic Vector Search
        if (request.UseSemanticSearch)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
            var vectorResults = await _vectorStore.SearchSimilarAsync(embedding, request.WorkspaceId, 5, cancellationToken);

            foreach (var vr in vectorResults)
            {
                if (vr.Metadata.TryGetValue("Title", out var titleObj) &&
                    vr.Metadata.TryGetValue("EntityId", out var idObj) &&
                    Guid.TryParse(idObj?.ToString(), out var entityId))
                {
                    results.Add(new SearchResultItem(
                        entityId,
                        vr.Metadata.TryGetValue("EntityType", out var typeObj) ? typeObj?.ToString() ?? "Knowledge" : "Knowledge",
                        titleObj?.ToString() ?? "Knowledge Item",
                        "Semantic match with score: " + vr.Score.ToString("F2"),
                        vr.Score,
                        DateTime.UtcNow));
                }
            }
        }

        var rankedItems = results
            .DistinctBy(r => r.EntityId)
            .OrderByDescending(r => r.Score)
            .Take(request.Limit)
            .ToList();

        return new SearchResponse(request.Query, rankedItems.Count, rankedItems);
    }
}
