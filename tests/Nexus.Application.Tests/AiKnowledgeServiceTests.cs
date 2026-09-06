using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.Features.AI.Services;
using Nexus.Application.Features.Notes.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class AiKnowledgeServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly User _testUser;
    private readonly User _otherUser;
    private readonly Workspace _workspaceA;
    private readonly Workspace _workspaceB;
    private readonly MockNoteService _mockNoteService;

    private class MockNoteService : INoteService
    {
        public List<NoteDto> CreatedNotes { get; } = new();
        public bool ShouldFail { get; set; } = false;

        public Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(new Error("Note.Failed", "Cannot create note.")));

            var note = new NoteDto(
                Guid.NewGuid(),
                workspaceId,
                request.PageId,
                "Page Title",
                request.Title,
                request.Content,
                request.ContentType,
                request.IsPinned,
                DateTime.UtcNow,
                null,
                request.Tags ?? new List<string>());

            CreatedNotes.Add(note);
            return Task.FromResult(Result.Success(note));
        }

        public Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private class FakeLlmService : ILLMService
    {
        public bool ShouldFail { get; set; } = false;
        public string ResponseToReturn { get; set; } = "Default AI generated answer.";
        public LLMRequest? LastRequest { get; set; }

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldFail) throw new LlmException("Simulated provider failure");
            LastRequest = request;
            return Task.FromResult(new LLMResponse(ResponseToReturn, "fake-llm", 50, 50, 100));
        }
    }

    public AiKnowledgeServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "AiKnowledgeTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        _testUser = new User { Email = "alice@nexus.ai", FullName = "Alice" };
        _otherUser = new User { Email = "bob@nexus.ai", FullName = "Bob" };
        _context.Users.AddRange(_testUser, _otherUser);

        _workspaceA = new Workspace { Name = "Workspace Alpha", OwnerId = _testUser.Id };
        _workspaceB = new Workspace { Name = "Workspace Beta", OwnerId = _otherUser.Id };
        _context.Workspaces.AddRange(_workspaceA, _workspaceB);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;

        _mockNoteService = new MockNoteService();
    }

    private AiKnowledgeService CreateService(FakeLlmService llm, AiKnowledgeOptions? options = null)
    {
        var opt = options ?? new AiKnowledgeOptions();
        return new AiKnowledgeService(
            _context,
            _currentUserService,
            llm,
            _mockNoteService,
            Options.Create(opt),
            Options.Create(new LlmOptions()),
            NullLogger<AiKnowledgeService>.Instance);
    }

    [Fact]
    public async Task SummarizeAsync_WithDocument_ExtractsBoundedChunks_ReturnsSummary()
    {
        // Arrange
        var doc = new Document
        {
            WorkspaceId = _workspaceA.Id,
            FileName = "Architecture.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            Title = "Architecture Guide",
            Status = DocumentStatus.Processed,
            ExtractedText = "Full document text"
        };
        _context.Documents.Add(doc);
        _context.SaveChanges();

        // Add 5 chunks
        for (int i = 0; i < 5; i++)
        {
            _context.DocumentChunks.Add(new DocumentChunk
            {
                DocumentId = doc.Id,
                WorkspaceId = _workspaceA.Id,
                ChunkIndex = i,
                Text = $"Chunk #{i} content for architecture testing. ",
                StartPosition = i * 40,
                EndPosition = (i + 1) * 40
            });
        }
        _context.SaveChanges();

        var llm = new FakeLlmService { ResponseToReturn = "## Executive Summary\nThe document covers system architecture." };
        var service = CreateService(llm);

        // Act
        var result = await service.SummarizeAsync(_workspaceA.Id, new AiKnowledgeRequest("Document", doc.Id, "Keep it concise"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Summarize", result.Value.Operation);
        Assert.Contains("Executive Summary", result.Value.Content);
        Assert.NotEmpty(result.Value.Sources);
        Assert.Equal(doc.Title, result.Value.Sources[0].Title);

        // Verify entity persisted in DB
        var persisted = await _context.AiGenerations
            .Include(g => g.Sources)
            .FirstOrDefaultAsync(g => g.Id == result.Value.Id);
        Assert.NotNull(persisted);
        Assert.Equal(_workspaceA.Id, persisted.WorkspaceId);
        Assert.Equal("Summarize", persisted.Operation);
        Assert.Equal(5, persisted.Sources.Count);
        Assert.Equal(doc.Id, persisted.Sources.First().DocumentId);
    }

    [Fact]
    public async Task SummarizeAsync_WithPage_BindsMaxCharacters()
    {
        // Arrange
        var longContent = new string('A', 20000);
        var page = new Page
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Long Specification Page",
            Icon = "📄",
            ContentJson = longContent
        };
        _context.Pages.Add(page);
        _context.SaveChanges();

        var llm = new FakeLlmService { ResponseToReturn = "Page summary." };
        var options = new AiKnowledgeOptions { MaxContextCharacters = 1000 };
        var service = CreateService(llm, options);

        // Act
        var result = await service.SummarizeAsync(_workspaceA.Id, new AiKnowledgeRequest("Page", page.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(llm.LastRequest);
        // User message should contain bounded content (at most 1000 chars of content)
        var userMsg = llm.LastRequest.Messages.Last().Content;
        Assert.DoesNotContain(new string('A', 1500), userMsg);
    }

    [Fact]
    public async Task ExplainAsync_WithNote_GeneratesExplanation()
    {
        // Arrange
        var note = new Note
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Vector Embeddings Note",
            Content = "Embeddings map high-dimensional semantic spaces to floating point vectors.",
            ContentType = "markdown"
        };
        _context.Notes.Add(note);
        _context.SaveChanges();

        var llm = new FakeLlmService { ResponseToReturn = "Explanation: Vector embeddings allow semantic search." };
        var service = CreateService(llm);

        // Act
        var result = await service.ExplainAsync(_workspaceA.Id, new AiKnowledgeRequest("Note", note.Id, "Explain for a 5-year-old"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Explain", result.Value.Operation);
        Assert.Contains("Vector embeddings", result.Value.Content);
        Assert.Contains("Explain for a 5-year-old", llm.LastRequest!.Messages.Last().Content);
    }

    [Fact]
    public async Task ExtractKeyPointsAsync_ExtractsBulletedPoints()
    {
        // Arrange
        var note = new Note
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Meeting Minutes",
            Content = "1. Ship Phase 5.\n2. Verify all tests pass.\n3. Celebrate.",
            ContentType = "markdown"
        };
        _context.Notes.Add(note);
        _context.SaveChanges();

        var llm = new FakeLlmService
        {
            ResponseToReturn = "• Ship Phase 5\n• Verify all tests pass\n• Celebrate"
        };
        var service = CreateService(llm);

        // Act
        var result = await service.ExtractKeyPointsAsync(_workspaceA.Id, new AiKnowledgeRequest("Note", note.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.KeyPoints);
        Assert.Equal(3, result.Value.KeyPoints.Count);
        Assert.Contains("Ship Phase 5", result.Value.KeyPoints[0]);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_StructuredJson_ValidParsing()
    {
        // Arrange
        var note = new Note
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Clean Architecture",
            Content = "Clean Architecture decouples business rules from external frameworks.",
            ContentType = "markdown"
        };
        _context.Notes.Add(note);
        _context.SaveChanges();

        var json = """
        [
          {
            "question": "What is the primary benefit of Clean Architecture?",
            "difficulty": "Intermediate",
            "answer": "Decoupling business rules from frameworks."
          },
          {
            "question": "Where do domain entities reside?",
            "difficulty": "Beginner",
            "answer": "In the core Domain layer."
          }
        ]
        """;

        var llm = new FakeLlmService { ResponseToReturn = $"```json\n{json}\n```" };
        var service = CreateService(llm);

        // Act
        var result = await service.GenerateQuestionsAsync(_workspaceA.Id, new GenerateQuestionsRequest("Note", note.Id, 2, "Intermediate"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Questions);
        Assert.Equal(2, result.Value.Questions.Count);
        Assert.Equal("What is the primary benefit of Clean Architecture?", result.Value.Questions[0].Question);
        Assert.Equal("Intermediate", result.Value.Questions[0].Difficulty);
        Assert.Equal("Decoupling business rules from frameworks.", result.Value.Questions[0].Answer);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_MalformedJson_FallsBackCleanlyWithoutCrashing()
    {
        // Arrange
        var note = new Note
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Database Indexing",
            Content = "B-Tree indexes speed up lookups.",
            ContentType = "markdown"
        };
        _context.Notes.Add(note);
        _context.SaveChanges();

        // LLM returns raw text or broken JSON
        var brokenText = """
        Here are your questions:
        1. How do B-Trees optimize lookup?
           Answer: By keeping data sorted in logarithmic depth.
        2. What is an index scan?
           Answer: Reading leaf nodes of an index.
        """;

        var llm = new FakeLlmService { ResponseToReturn = brokenText };
        var service = CreateService(llm);

        // Act
        var result = await service.GenerateQuestionsAsync(_workspaceA.Id, new GenerateQuestionsRequest("Note", note.Id, 2, "Intermediate"));

        // Assert - should NOT fail or throw, must provide clean fallback questions
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Questions);
        Assert.NotEmpty(result.Value.Questions);
        Assert.Contains("How do B-Trees optimize lookup?", result.Value.Questions[0].Question);
    }

    [Fact]
    public async Task GenerateStudyMaterialAsync_GeneratesCompleteGuide()
    {
        // Arrange
        var page = new Page
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Algorithms Chapter",
            ContentJson = "Sorting, searching, dynamic programming."
        };
        _context.Pages.Add(page);
        _context.SaveChanges();

        var llm = new FakeLlmService
        {
            ResponseToReturn = "# Study Guide: Algorithms\n\n## Key Concepts\n- Sorting\n- Searching"
        };
        var service = CreateService(llm);

        // Act
        var result = await service.GenerateStudyMaterialAsync(_workspaceA.Id, new GenerateStudyMaterialRequest("Page", page.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("StudyMaterial", result.Value.Operation);
        Assert.Contains("# Study Guide: Algorithms", result.Value.Content);
    }

    [Fact]
    public async Task SaveAsNoteAsync_ValidGenerationAndPage_ReusesNoteService()
    {
        // Arrange
        var page = new Page
        {
            WorkspaceId = _workspaceA.Id,
            Title = "Destination Study Page",
            ContentJson = "{}"
        };
        _context.Pages.Add(page);

        var generation = new AiGeneration
        {
            WorkspaceId = _workspaceA.Id,
            UserId = _testUser.Id,
            Operation = "Summarize",
            Content = "Synthesized knowledge output.",
            Model = "gpt-4o"
        };
        _context.AiGenerations.Add(generation);
        _context.SaveChanges();

        var service = CreateService(new FakeLlmService());

        // Act
        var result = await service.SaveAsNoteAsync(
            _workspaceA.Id,
            new SaveAiOutputAsNoteRequest(generation.Id, page.Id, "AI Note Title"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("AI Note Title", result.Value.Title);
        Assert.Contains("Synthesized knowledge output.", result.Value.Content);
        Assert.Contains("Generated by NEXUS AI", result.Value.Content);
        Assert.Single(_mockNoteService.CreatedNotes);
        Assert.Equal("AI Note Title", _mockNoteService.CreatedNotes[0].Title);
    }

    [Fact]
    public async Task SaveAsNoteAsync_DestinationPageInDifferentWorkspace_Fails()
    {
        // Arrange
        var pageInB = new Page
        {
            WorkspaceId = _workspaceB.Id,
            Title = "Page in Workspace B",
            ContentJson = "{}"
        };
        _context.Pages.Add(pageInB);

        var generation = new AiGeneration
        {
            WorkspaceId = _workspaceA.Id,
            UserId = _testUser.Id,
            Operation = "Summarize",
            Content = "Output",
            Model = "gpt-4o"
        };
        _context.AiGenerations.Add(generation);
        _context.SaveChanges();

        var service = CreateService(new FakeLlmService());

        // Act
        var result = await service.SaveAsNoteAsync(
            _workspaceA.Id,
            new SaveAiOutputAsNoteRequest(generation.Id, pageInB.Id, "Attempt Cross Workspace Note"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Note.PageNotFound", result.Error.Code);
    }

    [Fact]
    public async Task WorkspaceIsolation_SourceBelongsToDifferentWorkspace_Fails()
    {
        // Arrange: document in workspace B
        var docB = new Document
        {
            WorkspaceId = _workspaceB.Id,
            FileName = "SecretB.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            Title = "Secret B",
            Status = DocumentStatus.Processed
        };
        _context.Documents.Add(docB);
        _context.SaveChanges();

        var service = CreateService(new FakeLlmService());

        // Act: User in workspace A attempts to access document in workspace B
        var result = await service.SummarizeAsync(_workspaceA.Id, new AiKnowledgeRequest("Document", docB.Id));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Knowledge.SourceNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetAiGenerationsAsync_WorkspaceFiltered()
    {
        // Arrange
        var genA = new AiGeneration
        {
            WorkspaceId = _workspaceA.Id,
            UserId = _testUser.Id,
            Operation = "Summarize",
            Content = "rA",
            Model = "m"
        };
        var genB = new AiGeneration
        {
            WorkspaceId = _workspaceB.Id,
            UserId = _otherUser.Id,
            Operation = "Explain",
            Content = "rB",
            Model = "m"
        };
        _context.AiGenerations.AddRange(genA, genB);
        _context.SaveChanges();

        var service = CreateService(new FakeLlmService());

        // Act
        var result = await service.GetGenerationsAsync(_workspaceA.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(genA.Id, result.Value[0].Id);
    }

    [Fact]
    public async Task DeleteAiGenerationAsync_SoftDeletesAuditableEntity()
    {
        // Arrange
        var gen = new AiGeneration
        {
            WorkspaceId = _workspaceA.Id,
            UserId = _testUser.Id,
            Operation = "Summarize",
            Content = "r",
            Model = "m"
        };
        _context.AiGenerations.Add(gen);
        _context.SaveChanges();

        var service = CreateService(new FakeLlmService());

        // Act
        var result = await service.DeleteGenerationAsync(_workspaceA.Id, gen.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var deleted = await _context.AiGenerations.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == gen.Id);
        Assert.NotNull(deleted);
        Assert.True(deleted.IsDeleted);
    }
}
