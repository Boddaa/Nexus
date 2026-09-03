using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Options;
using Nexus.Application.Features.Documents.Services;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.AI.Chunking;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class DocumentChunkServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly DocumentChunkService _chunkService;
    private readonly User _testUser;
    private readonly Workspace _workspace;

    public DocumentChunkServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "ChunkServiceTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);

        var chunker = new DocumentChunker();
        var chunkingOptions = Options.Create(new ChunkingOptions { ChunkSize = 200, ChunkOverlap = 30 });

        _chunkService = new DocumentChunkService(_context, _currentUserService, chunker, chunkingOptions);

        _testUser = new User { Email = "author@nexus.ai", FullName = "Document Author" };
        _context.Users.Add(_testUser);

        _workspace = new Workspace { Name = "Knowledge Space", OwnerId = _testUser.Id };
        _context.Workspaces.Add(_workspace);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task ChunkDocumentAsync_ValidText_CreatesChunksWithPendingStatus()
    {
        var doc = new Document
        {
            WorkspaceId = _workspace.Id,
            Title = "AI Principles",
            FileName = "ai.txt",
            ExtractedText = "Artificial intelligence enables knowledge agents to process large document sets. " +
                            "This is a comprehensive overview of machine learning paradigms and prompt engineering.",
            Status = DocumentStatus.Processed,
            PageCount = 2
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var result = await _chunkService.ChunkDocumentAsync(_workspace.Id, doc.Id);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
        Assert.All(result.Value, c =>
        {
            Assert.Equal(doc.Id, c.DocumentId);
            Assert.Equal(_workspace.Id, c.WorkspaceId);
            Assert.Equal(EmbeddingStatus.Pending.ToString(), c.EmbeddingStatus);
            Assert.False(c.HasEmbedding);
        });

        // Verify persisted in database
        var dbChunks = await _context.DocumentChunks.Where(c => c.DocumentId == doc.Id).ToListAsync();
        Assert.Equal(result.Value.Count, dbChunks.Count);
    }

    [Fact]
    public async Task ChunkDocumentAsync_Rechunking_DeletesObsoleteChunksWithoutDuplicates()
    {
        var doc = new Document
        {
            WorkspaceId = _workspace.Id,
            Title = "Evolution of Computing",
            FileName = "evolution.md",
            ExtractedText = "Initial version text discussing the history of microprocessors.",
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // Run chunking pass 1
        var pass1 = await _chunkService.ChunkDocumentAsync(_workspace.Id, doc.Id);
        Assert.True(pass1.IsSuccess);
        var initialCount = await _context.DocumentChunks.CountAsync(c => c.DocumentId == doc.Id);

        // Update extracted text and re-chunk
        doc.ExtractedText = "Updated version text completely replacing the original manuscript with new content.";
        await _context.SaveChangesAsync();

        var pass2 = await _chunkService.ChunkDocumentAsync(_workspace.Id, doc.Id);

        Assert.True(pass2.IsSuccess);
        var afterRechunkCount = await _context.DocumentChunks.CountAsync(c => c.DocumentId == doc.Id);

        // Rechunking must not leave old chunks orphaned or duplicate chunks
        Assert.Equal(pass2.Value.Count, afterRechunkCount);
    }

    [Fact]
    public async Task ChunkDocumentAsync_EmptyExtractedText_ReturnsFailure()
    {
        var doc = new Document
        {
            WorkspaceId = _workspace.Id,
            Title = "Empty File",
            FileName = "empty.txt",
            ExtractedText = "",
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var result = await _chunkService.ChunkDocumentAsync(_workspace.Id, doc.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Document.NoText", result.Error.Code);
    }

    [Fact]
    public async Task ChunkDocumentAsync_UnauthorizedUser_ReturnsFailure()
    {
        var stranger = new User { Email = "intruder@nexus.ai", FullName = "Intruder" };
        _context.Users.Add(stranger);
        await _context.SaveChangesAsync();

        _currentUserService.UserId = stranger.Id;

        var doc = new Document
        {
            WorkspaceId = _workspace.Id,
            Title = "Secret Docs",
            FileName = "secret.txt",
            ExtractedText = "Confidential data.",
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var result = await _chunkService.ChunkDocumentAsync(_workspace.Id, doc.Id);

        Assert.False(result.IsSuccess);
    }
}
