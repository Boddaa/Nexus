using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.Common.Utilities;
using Nexus.Application.DTOs.Search;
using Nexus.Application.Features.Search.Services;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class HybridSearchTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly SearchService _searchService;
    private readonly User _testUser;
    private readonly Workspace _workspace;

    private class MockEmbeddingService : IEmbeddingService
    {
        public int EmbeddingDimension => 2;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            // Simple deterministic vector: [1.0, 0.0]
            return Task.FromResult(new float[] { 1.0f, 0.0f });
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var list = new List<float[]>();
            for (int i = 0; i < texts.Count; i++) list.Add(new float[] { 1.0f, 0.0f });
            return Task.FromResult<IReadOnlyList<float[]>>(list);
        }
    }

    public HybridSearchTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "HybridSearchTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        var hybridOptions = Options.Create(new HybridSearchOptions
        {
            KeywordWeight = 0.5,
            VectorWeight = 0.5,
            DefaultTopK = 10,
            MaxTopK = 50
        });

        var vectorSearchService = new VectorSearchService(_context, _currentUserService, hybridOptions);
        var mockEmbeddingService = new MockEmbeddingService();

        _searchService = new SearchService(_context, _currentUserService, vectorSearchService, mockEmbeddingService, hybridOptions);

        _testUser = new User { Email = "researcher@nexus.ai", FullName = "AI Researcher" };
        _context.Users.Add(_testUser);

        _workspace = new Workspace { Name = "Deep Learning Lab", OwnerId = _testUser.Id };
        _context.Workspaces.Add(_workspace);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task SearchAsync_KeywordMode_ReturnsKeywordResultsExclusively()
    {
        var page = new Page { WorkspaceId = _workspace.Id, Title = "Transformer Architecture", ContentJson = "Attention is all you need." };
        _context.Pages.Add(page);
        await _context.SaveChangesAsync();

        var request = new SearchRequest("Transformer", Mode: "Keyword");
        var result = await _searchService.SearchAsync(_workspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Keyword", result.Value.Items[0].SearchMode);
        Assert.Equal("Page", result.Value.Items[0].Type);
    }

    [Fact]
    public async Task SearchAsync_SemanticMode_ReturnsVectorChunkResults()
    {
        var doc = new Document { WorkspaceId = _workspace.Id, Title = "Deep Residual Learning", FileName = "resnet.pdf" };
        _context.Documents.Add(doc);

        var chunk = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspace.Id,
            ChunkIndex = 0,
            Text = "Residual networks facilitate the training of deeper neural networks.",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(new float[] { 1.0f, 0.0f }) // Perfect match to query
        };
        _context.DocumentChunks.Add(chunk);
        await _context.SaveChangesAsync();

        var request = new SearchRequest("Neural Networks", Mode: "Semantic");
        var result = await _searchService.SearchAsync(_workspace.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        var item = result.Value.Items[0];
        Assert.Equal("Semantic", item.SearchMode);
        Assert.Equal("Document", item.Type);
        Assert.Equal(doc.Id, item.Id);
        Assert.Equal(chunk.Id, item.ChunkId);
        Assert.NotNull(item.SimilarityScore);
    }

    [Fact]
    public async Task SearchAsync_HybridMode_DeduplicatesAndFusesScores()
    {
        // Document that matches BOTH by keyword title AND by vector embedding
        var doc = new Document
        {
            WorkspaceId = _workspace.Id,
            Title = "Transformer Mechanics",
            FileName = "transformer.pdf"
        };
        _context.Documents.Add(doc);

        var chunk = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspace.Id,
            ChunkIndex = 0,
            Text = "Self-attention layers compute representations across sequences.",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(new float[] { 1.0f, 0.0f })
        };
        _context.DocumentChunks.Add(chunk);
        await _context.SaveChangesAsync();

        var request = new SearchRequest("Transformer", Mode: "Hybrid");
        var result = await _searchService.SearchAsync(_workspace.Id, request);

        Assert.True(result.IsSuccess);
        // Should only have 1 deduplicated item for this document
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Hybrid", item.SearchMode);
        Assert.Equal(doc.Id, item.Id);
        Assert.Equal(chunk.Id, item.ChunkId);
        // Multi-signal match should have boosted score (> 50)
        Assert.True(item.Score > 50.0);
    }

    [Fact]
    public async Task SearchAsync_InvalidMode_ReturnsMeaningfulFailure()
    {
        var request = new SearchRequest("Test", Mode: "UnrecognizedMode");
        var result = await _searchService.SearchAsync(_workspace.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Search.InvalidMode", result.Error.Code);
    }
}
