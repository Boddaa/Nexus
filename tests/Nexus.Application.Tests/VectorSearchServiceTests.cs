using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Options;
using Nexus.Application.Common.Utilities;
using Nexus.Application.Features.Search.Services;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class VectorSearchServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly VectorSearchService _vectorSearchService;
    private readonly User _testUser;
    private readonly Workspace _workspaceA;
    private readonly Workspace _workspaceB;

    public VectorSearchServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "VectorSearchTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        var searchOptions = Options.Create(new HybridSearchOptions { DefaultTopK = 5, MaxTopK = 20 });
        _vectorSearchService = new VectorSearchService(_context, _currentUserService, searchOptions);

        _testUser = new User { Email = "analyst@nexus.ai", FullName = "Data Analyst" };
        _context.Users.Add(_testUser);

        _workspaceA = new Workspace { Name = "Workspace Alpha", OwnerId = _testUser.Id };
        _workspaceB = new Workspace { Name = "Workspace Beta", OwnerId = _testUser.Id };
        _context.Workspaces.AddRange(_workspaceA, _workspaceB);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task SearchSimilarChunksAsync_ReturnsTopKOrderedBySimilarity()
    {
        var doc = new Document { WorkspaceId = _workspaceA.Id, Title = "Tech Overview", FileName = "tech.txt" };
        _context.Documents.Add(doc);

        var targetQuery = new float[] { 1.0f, 0.0f, 0.0f };

        // Chunk 1: High similarity (1.0)
        var chunk1 = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 0,
            Text = "High match text",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(new float[] { 1.0f, 0.0f, 0.0f })
        };

        // Chunk 2: Medium similarity (0.7)
        var chunk2 = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 1,
            Text = "Medium match text",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(new float[] { 0.707f, 0.707f, 0.0f })
        };

        // Chunk 3: Low/Zero similarity (0.0)
        var chunk3 = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 2,
            Text = "Zero match text",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(new float[] { 0.0f, 1.0f, 0.0f })
        };

        _context.DocumentChunks.AddRange(chunk1, chunk2, chunk3);
        await _context.SaveChangesAsync();

        var result = await _vectorSearchService.SearchSimilarChunksAsync(_workspaceA.Id, targetQuery, topK: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(chunk1.Id, result.Value[0].ChunkId);
        Assert.True(result.Value[0].SimilarityScore > result.Value[1].SimilarityScore);
    }

    [Fact]
    public async Task SearchSimilarChunksAsync_StrictWorkspaceIsolation_ExcludesOtherWorkspaces()
    {
        var docA = new Document { WorkspaceId = _workspaceA.Id, Title = "Alpha Doc", FileName = "a.txt" };
        var docB = new Document { WorkspaceId = _workspaceB.Id, Title = "Beta Doc", FileName = "b.txt" };
        _context.Documents.AddRange(docA, docB);

        var vector = new float[] { 0.5f, 0.5f };

        var chunkA = new DocumentChunk
        {
            DocumentId = docA.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 0,
            Text = "Workspace A Chunk",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(vector)
        };

        var chunkB = new DocumentChunk
        {
            DocumentId = docB.Id,
            WorkspaceId = _workspaceB.Id,
            ChunkIndex = 0,
            Text = "Workspace B Chunk",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(vector)
        };

        _context.DocumentChunks.AddRange(chunkA, chunkB);
        await _context.SaveChangesAsync();

        var result = await _vectorSearchService.SearchSimilarChunksAsync(_workspaceA.Id, vector, topK: 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(_workspaceA.Id, result.Value[0].WorkspaceId);
        Assert.Equal(chunkA.Id, result.Value[0].ChunkId);
    }

    [Fact]
    public async Task SearchSimilarChunksAsync_ExcludesSoftDeletedDocumentsAndChunks()
    {
        var activeDoc = new Document { WorkspaceId = _workspaceA.Id, Title = "Active", FileName = "active.txt", IsDeleted = false };
        var deletedDoc = new Document { WorkspaceId = _workspaceA.Id, Title = "Deleted", FileName = "deleted.txt", IsDeleted = true };
        _context.Documents.AddRange(activeDoc, deletedDoc);

        var vector = new float[] { 1.0f, 0.0f };

        var activeChunk = new DocumentChunk
        {
            DocumentId = activeDoc.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 0,
            Text = "Active Chunk",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(vector),
            IsDeleted = false
        };

        var deletedDocChunk = new DocumentChunk
        {
            DocumentId = deletedDoc.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 0,
            Text = "Chunk of deleted doc",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(vector),
            IsDeleted = false
        };

        var deletedChunk = new DocumentChunk
        {
            DocumentId = activeDoc.Id,
            WorkspaceId = _workspaceA.Id,
            ChunkIndex = 1,
            Text = "Soft deleted chunk directly",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingVector = VectorMath.VectorToBytes(vector),
            IsDeleted = true
        };

        _context.DocumentChunks.AddRange(activeChunk, deletedDocChunk, deletedChunk);
        await _context.SaveChangesAsync();

        var result = await _vectorSearchService.SearchSimilarChunksAsync(_workspaceA.Id, vector, topK: 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(activeChunk.Id, result.Value[0].ChunkId);
    }
}
