using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Search;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.Search.Services;

public class SearchService : ISearchService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IOptions<HybridSearchOptions> _hybridOptions;

    public SearchService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IVectorSearchService vectorSearchService,
        IEmbeddingService embeddingService,
        IOptions<HybridSearchOptions> hybridOptions)
    {
        _context = context;
        _currentUserService = currentUserService;
        _vectorSearchService = vectorSearchService;
        _embeddingService = embeddingService;
        _hybridOptions = hybridOptions;
    }

    public async Task<Result<PagedResult<SearchResultDto>>> SearchAsync(
        Guid workspaceId,
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticate user
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<PagedResult<SearchResultDto>>(Error.Unauthorized);
        }

        // 2. Validate workspace access
        var isMember = await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                           (w.OwnerId == currentUserId.Value ||
                            w.Members.Any(m => m.UserId == currentUserId.Value && !m.IsDeleted)),
                      cancellationToken);

        if (!isMember)
        {
            return Result.Failure<PagedResult<SearchResultDto>>(Error.Unauthorized);
        }

        // 3. Validate Query
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Result.Failure<PagedResult<SearchResultDto>>(
                new Error("Search.EmptyQuery", "Search query cannot be empty."));
        }

        var trimmedQuery = request.Query.Trim();
        if (trimmedQuery.Length > 200)
        {
            return Result.Failure<PagedResult<SearchResultDto>>(
                new Error("Search.QueryTooLong", "Search query cannot exceed 200 characters."));
        }

        // 4. Validate and normalize pagination
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : (request.PageSize > 50 ? 50 : request.PageSize);

        // 5. Validate Search Mode
        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "Keyword" : request.Mode.Trim();
        if (!mode.Equals("Keyword", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("Semantic", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("Hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<PagedResult<SearchResultDto>>(
                new Error("Search.InvalidMode", $"Invalid search mode '{mode}'. Allowed values: Keyword, Semantic, Hybrid."));
        }

        // 6. Dispatch according to Mode
        if (mode.Equals("Semantic", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteSemanticSearchAsync(workspaceId, trimmedQuery, page, pageSize, request, cancellationToken);
        }

        if (mode.Equals("Hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteHybridSearchAsync(workspaceId, trimmedQuery, page, pageSize, request, cancellationToken);
        }

        // Default: Keyword search
        return await ExecuteKeywordSearchAsync(workspaceId, trimmedQuery, page, pageSize, request, cancellationToken);
    }

    #region Keyword Search (v1 Preserved)

    private async Task<Result<PagedResult<SearchResultDto>>> ExecuteKeywordSearchAsync(
        Guid workspaceId,
        string trimmedQuery,
        int page,
        int pageSize,
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // Validate Type filter
        var typeFilter = request.Type?.Trim();
        bool searchPages = true;
        bool searchNotes = true;
        bool searchDocs = true;

        if (!string.IsNullOrWhiteSpace(typeFilter) && !typeFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (typeFilter.Equals("Page", StringComparison.OrdinalIgnoreCase))
            {
                searchNotes = false;
                searchDocs = false;
            }
            else if (typeFilter.Equals("Note", StringComparison.OrdinalIgnoreCase))
            {
                searchPages = false;
                searchDocs = false;
            }
            else if (typeFilter.Equals("Document", StringComparison.OrdinalIgnoreCase))
            {
                searchPages = false;
                searchNotes = false;
            }
            else
            {
                return Result.Failure<PagedResult<SearchResultDto>>(
                    new Error("Search.InvalidType", $"Invalid search type filter '{typeFilter}'. Allowed values: Page, Note, Document."));
            }
        }

        var tokens = trimmedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fullPattern = $"%{trimmedQuery}%";

        // Construct DB-side predicates
        Expression<Func<Page, bool>>? pagePredicate = null;
        if (searchPages)
        {
            pagePredicate = PredicateBuilder.False<Page>();
            pagePredicate = pagePredicate.Or(p => EF.Functions.Like(p.Title, fullPattern) || EF.Functions.Like(p.ContentJson, fullPattern));
            foreach (var token in tokens.Take(5))
            {
                var tokenPattern = $"%{token}%";
                pagePredicate = pagePredicate.Or(p => EF.Functions.Like(p.Title, tokenPattern) || EF.Functions.Like(p.ContentJson, tokenPattern));
            }
        }

        Expression<Func<Note, bool>>? notePredicate = null;
        if (searchNotes)
        {
            notePredicate = PredicateBuilder.False<Note>();
            notePredicate = notePredicate.Or(n => EF.Functions.Like(n.Title, fullPattern) ||
                                                 EF.Functions.Like(n.Content, fullPattern) ||
                                                 n.Tags.Any(t => EF.Functions.Like(t.Name, fullPattern)));
            foreach (var token in tokens.Take(5))
            {
                var tokenPattern = $"%{token}%";
                notePredicate = notePredicate.Or(n => EF.Functions.Like(n.Title, tokenPattern) ||
                                                      EF.Functions.Like(n.Content, tokenPattern) ||
                                                      n.Tags.Any(t => EF.Functions.Like(t.Name, tokenPattern)));
            }
        }

        Expression<Func<Document, bool>>? docPredicate = null;
        if (searchDocs)
        {
            docPredicate = PredicateBuilder.False<Document>();
            docPredicate = docPredicate.Or(d => EF.Functions.Like(d.Title, fullPattern) ||
                                                EF.Functions.Like(d.FileName, fullPattern) ||
                                                EF.Functions.Like(d.ExtractedText, fullPattern));
            foreach (var token in tokens.Take(5))
            {
                var tokenPattern = $"%{token}%";
                docPredicate = docPredicate.Or(d => EF.Functions.Like(d.Title, tokenPattern) ||
                                                    EF.Functions.Like(d.FileName, tokenPattern) ||
                                                    EF.Functions.Like(d.ExtractedText, tokenPattern));
            }
        }

        // DB-Side CountAsync() for True Total Count
        int totalCount = 0;
        if (searchPages && pagePredicate != null)
        {
            totalCount += await _context.Pages
                .AsNoTracking()
                .Where(p => p.WorkspaceId == workspaceId && !p.IsDeleted)
                .Where(pagePredicate)
                .CountAsync(cancellationToken);
        }

        if (searchNotes && notePredicate != null)
        {
            totalCount += await _context.Notes
                .AsNoTracking()
                .Where(n => n.WorkspaceId == workspaceId && !n.IsDeleted)
                .Where(notePredicate)
                .CountAsync(cancellationToken);
        }

        if (searchDocs && docPredicate != null)
        {
            totalCount += await _context.Documents
                .AsNoTracking()
                .Where(d => d.WorkspaceId == workspaceId && !d.IsDeleted)
                .Where(docPredicate)
                .CountAsync(cancellationToken);
        }

        if (totalCount == 0)
        {
            return Result.Success(new PagedResult<SearchResultDto>(
                Array.Empty<SearchResultDto>(), page, pageSize, 0));
        }

        // Stage 1: Bounded Lightweight Candidate Retrieval
        var candidateLimit = Math.Max(pageSize, page * pageSize + 50);
        var candidates = new List<SearchCandidate>();

        if (searchPages && pagePredicate != null)
        {
            var matchingPages = await _context.Pages
                .AsNoTracking()
                .Where(p => p.WorkspaceId == workspaceId && !p.IsDeleted)
                .Where(pagePredicate)
                .Select(p => new { p.Id, p.Title, p.CreatedAtUtc, p.UpdatedAtUtc })
                .Take(candidateLimit)
                .ToListAsync(cancellationToken);

            foreach (var p in matchingPages)
            {
                var score = CalculateScore(trimmedQuery, tokens, p.Title, null);
                candidates.Add(new SearchCandidate(p.Id, "Page", workspaceId, null, p.Title, null, score == 0 ? 35 : score, p.CreatedAtUtc, p.UpdatedAtUtc));
            }
        }

        if (searchNotes && notePredicate != null)
        {
            var matchingNotes = await _context.Notes
                .AsNoTracking()
                .Where(n => n.WorkspaceId == workspaceId && !n.IsDeleted)
                .Where(notePredicate)
                .Select(n => new { n.Id, n.PageId, n.Title, TagNames = n.Tags.Select(t => t.Name).ToList(), n.CreatedAtUtc, n.UpdatedAtUtc })
                .Take(candidateLimit)
                .ToListAsync(cancellationToken);

            foreach (var n in matchingNotes)
            {
                var tagsString = string.Join(" ", n.TagNames);
                var score = CalculateScore(trimmedQuery, tokens, n.Title, tagsString);
                candidates.Add(new SearchCandidate(n.Id, "Note", workspaceId, n.PageId, n.Title, tagsString, score == 0 ? 35 : score, n.CreatedAtUtc, n.UpdatedAtUtc));
            }
        }

        if (searchDocs && docPredicate != null)
        {
            var matchingDocs = await _context.Documents
                .AsNoTracking()
                .Where(d => d.WorkspaceId == workspaceId && !d.IsDeleted)
                .Where(docPredicate)
                .Select(d => new { d.Id, d.PageId, d.Title, d.FileName, d.CreatedAtUtc, d.UpdatedAtUtc })
                .Take(candidateLimit)
                .ToListAsync(cancellationToken);

            foreach (var d in matchingDocs)
            {
                var score = CalculateScore(trimmedQuery, tokens, d.Title, d.FileName);
                candidates.Add(new SearchCandidate(d.Id, "Document", workspaceId, d.PageId, d.Title, d.FileName, score == 0 ? 35 : score, d.CreatedAtUtc, d.UpdatedAtUtc));
            }
        }

        // Sort candidates
        var sortedCandidates = candidates
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.CreatedAtUtc)
            .ToList();

        var pagedSlice = sortedCandidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Stage 2: Materialize Text ONLY for displayed page items
        Dictionary<Guid, string> docTexts = new();
        var pagedDocIds = pagedSlice.Where(c => c.Type == "Document").Select(c => c.Id).ToList();
        if (pagedDocIds.Count > 0)
        {
            docTexts = await _context.Documents
                .AsNoTracking()
                .Where(d => pagedDocIds.Contains(d.Id))
                .Select(d => new { d.Id, d.ExtractedText })
                .ToDictionaryAsync(d => d.Id, d => d.ExtractedText ?? string.Empty, cancellationToken);
        }

        Dictionary<Guid, string> pageTexts = new();
        var pagedPageIds = pagedSlice.Where(c => c.Type == "Page").Select(c => c.Id).ToList();
        if (pagedPageIds.Count > 0)
        {
            pageTexts = await _context.Pages
                .AsNoTracking()
                .Where(p => pagedPageIds.Contains(p.Id))
                .Select(p => new { p.Id, p.ContentJson })
                .ToDictionaryAsync(p => p.Id, p => CleanContent(p.ContentJson), cancellationToken);
        }

        Dictionary<Guid, string> noteTexts = new();
        var pagedNoteIds = pagedSlice.Where(c => c.Type == "Note").Select(c => c.Id).ToList();
        if (pagedNoteIds.Count > 0)
        {
            noteTexts = await _context.Notes
                .AsNoTracking()
                .Where(n => pagedNoteIds.Contains(n.Id))
                .Select(n => new { n.Id, n.Content })
                .ToDictionaryAsync(n => n.Id, n => n.Content ?? string.Empty, cancellationToken);
        }

        var finalResults = new List<SearchResultDto>(pagedSlice.Count);
        foreach (var c in pagedSlice)
        {
            string content = c.Type switch
            {
                "Document" => docTexts.GetValueOrDefault(c.Id, string.Empty),
                "Page" => pageTexts.GetValueOrDefault(c.Id, string.Empty),
                "Note" => noteTexts.GetValueOrDefault(c.Id, string.Empty),
                _ => string.Empty
            };

            var snippet = GenerateSnippet(content, trimmedQuery, tokens);

            finalResults.Add(new SearchResultDto(
                c.Id,
                c.Type,
                c.WorkspaceId,
                c.PageId,
                c.Title,
                snippet,
                c.Score,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                null,
                "Keyword",
                null));
        }

        return Result.Success(new PagedResult<SearchResultDto>(finalResults, page, pageSize, totalCount));
    }

    #endregion

    #region Semantic Search

    private async Task<Result<PagedResult<SearchResultDto>>> ExecuteSemanticSearchAsync(
        Guid workspaceId,
        string trimmedQuery,
        int page,
        int pageSize,
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // Type filter check: Semantic search indexes Document chunks
        var typeFilter = request.Type?.Trim();
        if (!string.IsNullOrWhiteSpace(typeFilter) &&
            !typeFilter.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            !typeFilter.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            // If user specifically asked for Pages or Notes in Semantic mode, currently returns empty
            return Result.Success(new PagedResult<SearchResultDto>(Array.Empty<SearchResultDto>(), page, pageSize, 0));
        }

        float[] queryVector;
        try
        {
            queryVector = await _embeddingService.GenerateEmbeddingAsync(trimmedQuery, cancellationToken);
        }
        catch (EmbeddingException ex)
        {
            return Result.Failure<PagedResult<SearchResultDto>>(
                new Error("Search.EmbeddingError", $"Failed to generate embedding for query: {ex.Message}"));
        }

        var topK = request.TopK ?? _hybridOptions.Value.DefaultTopK;
        var vectorResult = await _vectorSearchService.SearchSimilarChunksAsync(workspaceId, queryVector, topK, cancellationToken);
        if (!vectorResult.IsSuccess)
        {
            return Result.Failure<PagedResult<SearchResultDto>>(vectorResult.Error);
        }

        var semanticResults = vectorResult.Value.Select(c => new SearchResultDto(
            c.DocumentId,
            "Document",
            workspaceId,
            null,
            c.DocumentTitle,
            c.Text,
            Math.Max(0.0, c.SimilarityScore) * 100.0,
            c.CreatedAtUtc,
            null,
            c.ChunkId,
            "Semantic",
            c.SimilarityScore
        )).ToList();

        var totalCount = semanticResults.Count;
        var pagedItems = semanticResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result.Success(new PagedResult<SearchResultDto>(pagedItems, page, pageSize, totalCount));
    }

    #endregion

    #region Hybrid Search (Keyword + Semantic Fusion)

    private async Task<Result<PagedResult<SearchResultDto>>> ExecuteHybridSearchAsync(
        Guid workspaceId,
        string trimmedQuery,
        int page,
        int pageSize,
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Run keyword search (top 50 candidate pool)
        var keywordReq = new SearchRequest(trimmedQuery, 1, 50, request.Type, "Keyword");
        var keywordRes = await ExecuteKeywordSearchAsync(workspaceId, trimmedQuery, 1, 50, keywordReq, cancellationToken);

        var keywordItems = keywordRes.IsSuccess ? keywordRes.Value.Items : Array.Empty<SearchResultDto>();

        // 2. Run semantic vector search
        var topK = request.TopK ?? _hybridOptions.Value.DefaultTopK;
        IReadOnlyList<SearchResultDto> semanticItems = Array.Empty<SearchResultDto>();

        try
        {
            var queryVector = await _embeddingService.GenerateEmbeddingAsync(trimmedQuery, cancellationToken);
            var vectorRes = await _vectorSearchService.SearchSimilarChunksAsync(workspaceId, queryVector, topK, cancellationToken);
            if (vectorRes.IsSuccess)
            {
                semanticItems = vectorRes.Value.Select(c => new SearchResultDto(
                    c.DocumentId,
                    "Document",
                    workspaceId,
                    null,
                    c.DocumentTitle,
                    c.Text,
                    Math.Max(0.0, c.SimilarityScore) * 100.0,
                    c.CreatedAtUtc,
                    null,
                    c.ChunkId,
                    "Semantic",
                    c.SimilarityScore,
                    c.PageNumber
                )).ToList();
            }
        }
        catch (Exception)
        {
            // If embeddings are unconfigured, fall back gracefully to keyword search in hybrid mode
        }

        // 3. Score Normalization & Combination (Patch Rule 6)
        double maxKeywordScore = keywordItems.Count > 0 ? keywordItems.Max(k => k.Score) : 100.0;
        if (maxKeywordScore <= 0.0) maxKeywordScore = 1.0;

        var keywordWeight = _hybridOptions.Value.KeywordWeight;
        var vectorWeight = _hybridOptions.Value.VectorWeight;

        // Key: (Type, EntityId)
        var merged = new Dictionary<(string Type, Guid Id), SearchResultDto>();

        // Add normalized keyword items
        foreach (var kw in keywordItems)
        {
            var normalizedKwScore = (kw.Score / maxKeywordScore) * 100.0;
            var weightedScore = normalizedKwScore * keywordWeight;

            merged[(kw.Type, kw.Id)] = new SearchResultDto(
                kw.Id,
                kw.Type,
                kw.WorkspaceId,
                kw.PageId,
                kw.Title,
                kw.Snippet,
                weightedScore,
                kw.CreatedAtUtc,
                kw.UpdatedAtUtc,
                null,
                "Keyword",
                null);
        }

        // Merge normalized semantic items
        foreach (var sem in semanticItems)
        {
            var key = ("Document", sem.Id);
            var normalizedSemScore = Math.Max(0.0, sem.SimilarityScore ?? 0.0) * 100.0;
            var weightedSemScore = normalizedSemScore * vectorWeight;

            if (merged.TryGetValue(key, out var existing))
            {
                // Multi-signal match boost (+10)
                var combinedScore = existing.Score + weightedSemScore + 10.0;

                merged[key] = new SearchResultDto(
                    existing.Id,
                    existing.Type,
                    existing.WorkspaceId,
                    existing.PageId,
                    existing.Title,
                    sem.Snippet, // Chunk snippet is more specific
                    combinedScore,
                    existing.CreatedAtUtc,
                    existing.UpdatedAtUtc,
                    sem.ChunkId,
                    "Hybrid",
                    sem.SimilarityScore,
                    sem.PageNumber);
            }
            else
            {
                merged[key] = new SearchResultDto(
                    sem.Id,
                    "Document",
                    sem.WorkspaceId,
                    null,
                    sem.Title,
                    sem.Snippet,
                    weightedSemScore,
                    sem.CreatedAtUtc,
                    sem.UpdatedAtUtc,
                    sem.ChunkId,
                    "Semantic",
                    sem.SimilarityScore,
                    sem.PageNumber);
            }
        }

        var sortedResults = merged.Values
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.CreatedAtUtc)
            .ToList();

        var totalCount = sortedResults.Count;
        var pagedItems = sortedResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result.Success(new PagedResult<SearchResultDto>(pagedItems, page, pageSize, totalCount));
    }

    #endregion

    #region Ranking & Snippet Helpers

    private static double CalculateScore(
        string fullQuery,
        string[] tokens,
        string title,
        string? secondary)
    {
        double score = 0;

        if (!string.IsNullOrEmpty(title))
        {
            if (title.Equals(fullQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 150;
            }
            else if (title.Contains(fullQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            foreach (var token in tokens)
            {
                if (title.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 40;
                }
            }
        }

        if (!string.IsNullOrEmpty(secondary))
        {
            if (secondary.Contains(fullQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 60;
            }

            foreach (var token in tokens)
            {
                if (secondary.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 25;
                }
            }
        }

        return score;
    }

    private static string GenerateSnippet(string content, string query, string[] tokens, int maxLength = 250)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var matchIndex = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        if (matchIndex < 0)
        {
            foreach (var token in tokens)
            {
                var tokenIndex = content.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (tokenIndex >= 0)
                {
                    matchIndex = tokenIndex;
                    break;
                }
            }
        }

        if (matchIndex < 0)
        {
            if (content.Length <= maxLength)
            {
                return content.Trim();
            }

            return content[..maxLength].TrimEnd() + "...";
        }

        const int leadingContext = 40;
        var startIndex = Math.Max(0, matchIndex - leadingContext);
        var length = Math.Min(content.Length - startIndex, maxLength);

        var snippet = content.Substring(startIndex, length).Trim();

        if (startIndex > 0)
        {
            snippet = "..." + snippet;
        }

        if (startIndex + length < content.Length)
        {
            snippet += "...";
        }

        return snippet;
    }

    private static string CleanContent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (!raw.StartsWith("{") && !raw.StartsWith("[")) return raw;

        var cleaned = Regex.Replace(raw, @"[""{}\[\]:,]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    #endregion

    private record SearchCandidate(
        Guid Id,
        string Type,
        Guid WorkspaceId,
        Guid? PageId,
        string Title,
        string? Secondary,
        double Score,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);
}

internal static class PredicateBuilder
{
    public static Expression<Func<T, bool>> False<T>() => _ => false;

    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));

        var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
        var left = leftVisitor.Visit(expr1.Body)!;

        var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
        var right = rightVisitor.Visit(expr2.Body)!;

        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left, right), parameter);
    }

    private class ReplaceExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldValue;
        private readonly Expression _newValue;

        public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
        {
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public override Expression Visit(Expression? node)
        {
            return node == _oldValue ? _newValue : base.Visit(node)!;
        }
    }
}
