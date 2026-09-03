using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.Common.Utilities;
using Nexus.Application.Features.Documents.Services;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class ChunkEmbeddingServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly User _testUser;
    private readonly Workspace _workspace;
    private readonly EmbeddingOptions _options;

    private class MockEmbeddingService : IEmbeddingService
    {
        public int EmbeddingDimension { get; set; } = 4;
        public bool ShouldThrow { get; set; } = false;
        public int ReturnDimension { get; set; } = 4;

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow) throw new EmbeddingException("Provider unavailable.");
            return Task.FromResult(new float[ReturnDimension]);
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow) throw new EmbeddingException("Provider unavailable.");
            var list = new List<float[]>();
            for (int i = 0; i < texts.Count; i++)
            {
                var vec = new float[ReturnDimension];
                for (int d = 0; d < ReturnDimension; d++) vec[d] = 0.5f;
                list.Add(vec);
            }
            return Task.FromResult<IReadOnlyList<float[]>>(list);
        }
    }

    public ChunkEmbeddingServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "ChunkEmbedTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        _options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            Dimensions = 4
        };

        _testUser = new User { Email = "engineer@nexus.ai", FullName = "AI Engineer" };
        _context.Users.Add(_testUser);

        _workspace = new Workspace { Name = "AI Workspace", OwnerId = _testUser.Id };
        _context.Workspaces.Add(_workspace);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task GenerateEmbeddingsForDocumentAsync_ValidChunks_SetsCompletedAndStoresVectors()
    {
        var doc = new Document { WorkspaceId = _workspace.Id, Title = "Manual", FileName = "manual.txt" };
        _context.Documents.Add(doc);

        var chunk1 = new DocumentChunk { DocumentId = doc.Id, WorkspaceId = _workspace.Id, ChunkIndex = 0, Text = "Chunk 1", EmbeddingStatus = EmbeddingStatus.Pending };
        var chunk2 = new DocumentChunk { DocumentId = doc.Id, WorkspaceId = _workspace.Id, ChunkIndex = 1, Text = "Chunk 2", EmbeddingStatus = EmbeddingStatus.Pending };
        _context.DocumentChunks.AddRange(chunk1, chunk2);
        await _context.SaveChangesAsync();

        var mockEmbedding = new MockEmbeddingService { EmbeddingDimension = 4, ReturnDimension = 4 };
        var service = new ChunkEmbeddingService(_context, _currentUserService, mockEmbedding, Options.Create(_options), NullLogger<ChunkEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsForDocumentAsync(_workspace.Id, doc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalEmbeddingsGenerated);

        var updatedChunks = await _context.DocumentChunks.Where(c => c.DocumentId == doc.Id).ToListAsync();
        Assert.All(updatedChunks, c =>
        {
            Assert.Equal(EmbeddingStatus.Completed, c.EmbeddingStatus);
            Assert.NotNull(c.EmbeddingVector);
            Assert.Equal(4 * sizeof(float), c.EmbeddingVector.Length);
            Assert.Equal(_options.Model, c.EmbeddingModel);
            Assert.Equal(_options.Dimensions, c.EmbeddingDimensions);
        });
    }

    [Fact]
    public async Task GenerateEmbeddingsForDocumentAsync_Idempotency_SkipsAlreadyCompletedChunks()
    {
        var doc = new Document { WorkspaceId = _workspace.Id, Title = "Completed Doc", FileName = "doc.txt" };
        _context.Documents.Add(doc);

        var existingVector = VectorMath.VectorToBytes(new float[] { 0.1f, 0.2f, 0.3f, 0.4f });
        var chunk = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspace.Id,
            ChunkIndex = 0,
            Text = "Already done",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingModel = _options.Model,
            EmbeddingDimensions = _options.Dimensions,
            EmbeddingVector = existingVector
        };
        _context.DocumentChunks.Add(chunk);
        await _context.SaveChangesAsync();

        var mockEmbedding = new MockEmbeddingService { EmbeddingDimension = 4 };
        var service = new ChunkEmbeddingService(_context, _currentUserService, mockEmbedding, Options.Create(_options), NullLogger<ChunkEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsForDocumentAsync(_workspace.Id, doc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalEmbeddingsGenerated);
    }

    [Fact]
    public async Task GenerateEmbeddingsForDocumentAsync_StaleModelOrDimensions_RegeneratesEmbeddings()
    {
        var doc = new Document { WorkspaceId = _workspace.Id, Title = "Stale Doc", FileName = "stale.txt" };
        _context.Documents.Add(doc);

        var chunk = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspace.Id,
            ChunkIndex = 0,
            Text = "Needs re-embedding with new model",
            EmbeddingStatus = EmbeddingStatus.Completed,
            EmbeddingModel = "old-deprecated-model",
            EmbeddingDimensions = 4,
            EmbeddingVector = VectorMath.VectorToBytes(new float[] { 0, 0, 0, 0 })
        };
        _context.DocumentChunks.Add(chunk);
        await _context.SaveChangesAsync();

        var mockEmbedding = new MockEmbeddingService { EmbeddingDimension = 4 };
        var service = new ChunkEmbeddingService(_context, _currentUserService, mockEmbedding, Options.Create(_options), NullLogger<ChunkEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsForDocumentAsync(_workspace.Id, doc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalEmbeddingsGenerated);

        var reloaded = await _context.DocumentChunks.FindAsync(chunk.Id);
        Assert.Equal(_options.Model, reloaded!.EmbeddingModel);
    }

    [Fact]
    public async Task GenerateEmbeddingsForDocumentAsync_DimensionMismatch_FailsAndMarksChunksFailed()
    {
        var doc = new Document { WorkspaceId = _workspace.Id, Title = "Doc", FileName = "doc.txt" };
        _context.Documents.Add(doc);

        var chunk = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspace.Id,
            ChunkIndex = 0,
            Text = "Mismatched dimensions",
            EmbeddingStatus = EmbeddingStatus.Pending
        };
        _context.DocumentChunks.Add(chunk);
        await _context.SaveChangesAsync();

        // Return dimension 8 while options expect 4
        var mockEmbedding = new MockEmbeddingService { EmbeddingDimension = 4, ReturnDimension = 8 };
        var service = new ChunkEmbeddingService(_context, _currentUserService, mockEmbedding, Options.Create(_options), NullLogger<ChunkEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsForDocumentAsync(_workspace.Id, doc.Id);

        Assert.False(result.IsSuccess);

        var failedChunk = await _context.DocumentChunks.FindAsync(chunk.Id);
        Assert.Equal(EmbeddingStatus.Failed, failedChunk!.EmbeddingStatus);
    }

    [Fact]
    public async Task GenerateEmbeddingsForDocumentAsync_ProviderFailure_MarksChunksFailedWithoutSwallowing()
    {
        var doc = new Document { WorkspaceId = _workspace.Id, Title = "Fail Doc", FileName = "fail.txt" };
        _context.Documents.Add(doc);

        var chunk = new DocumentChunk
        {
            DocumentId = doc.Id,
            WorkspaceId = _workspace.Id,
            ChunkIndex = 0,
            Text = "Will fail",
            EmbeddingStatus = EmbeddingStatus.Pending
        };
        _context.DocumentChunks.Add(chunk);
        await _context.SaveChangesAsync();

        var mockEmbedding = new MockEmbeddingService { ShouldThrow = true };
        var service = new ChunkEmbeddingService(_context, _currentUserService, mockEmbedding, Options.Create(_options), NullLogger<ChunkEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsForDocumentAsync(_workspace.Id, doc.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Provider unavailable", result.Error.Description);

        var reloaded = await _context.DocumentChunks.FindAsync(chunk.Id);
        Assert.Equal(EmbeddingStatus.Failed, reloaded!.EmbeddingStatus);
    }
}
