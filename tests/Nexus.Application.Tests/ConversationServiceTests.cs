using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.Features.Conversations.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class ConversationServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly User _testUser;
    private readonly User _otherUser;
    private readonly Workspace _workspaceA;
    private readonly Workspace _workspaceB;

    private class MockRagService : IRagService
    {
        public bool ShouldFail { get; set; } = false;
        public string ResponseText { get; set; } = "Grounded response.";
        public List<ChatSourceDto> Sources { get; set; } = new();

        public Task<Result<RagAnswerResult>> AnswerQuestionAsync(
            Guid workspaceId,
            string question,
            IReadOnlyList<LLMChatMessage>? conversationHistory = null,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult(Result.Failure<RagAnswerResult>(new Error("AI.ProviderError", "LLM timeout.")));
            }

            return Task.FromResult(Result.Success(new RagAnswerResult(ResponseText, Sources, 50, 25, 75)));
        }

        public Task<RagResponse> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagResponse(ResponseText, Array.Empty<SourceReferenceDto>(), 0, 0));
        }
    }

    public ConversationServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "ConvTests_" + Guid.NewGuid().ToString())
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
    }

    [Fact]
    public async Task CreateConversationAsync_AuthorizedUser_CreatesSuccessfully()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var result = await service.CreateConversationAsync(_workspaceA.Id, new CreateConversationRequest("Project Q&A"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Project Q&A", result.Value.Title);
        Assert.Equal(_workspaceA.Id, result.Value.WorkspaceId);
        Assert.Equal(_testUser.Id, result.Value.UserId);
        Assert.False(result.Value.IsArchived);
    }

    [Fact]
    public async Task CreateConversationAsync_ForeignWorkspace_ReturnsUnauthorized()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var result = await service.CreateConversationAsync(_workspaceB.Id, new CreateConversationRequest("Intrusion"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Workspace.AccessDenied", result.Error.Code);
    }

    [Fact]
    public async Task ListConversationsAsync_UserIsolation_ReturnsOnlyCurrentUserConversations()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        // Add conversation for Alice in Workspace A
        var convAlice = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "Alice Chat" };
        // Add conversation for Bob in Workspace A (as member)
        _workspaceA.Members.Add(new WorkspaceMember { UserId = _otherUser.Id, WorkspaceId = _workspaceA.Id });
        var convBob = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _otherUser.Id, Title = "Bob Chat" };

        _context.AiConversations.AddRange(convAlice, convBob);
        await _context.SaveChangesAsync();

        var result = await service.ListConversationsAsync(_workspaceA.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Alice Chat", result.Value[0].Title);
    }

    [Fact]
    public async Task SendChatMessageAsync_Valid_SavesUserAndAssistantMessagesAndSources()
    {
        var mockRag = new MockRagService
        {
            ResponseText = "The architecture pattern is modular monolith.",
            Sources = new List<ChatSourceDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), null, null, null, "Arch.pdf", "Document", 0.95, 1, "Modular monolith structure.")
            }
        };

        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "New Conversation" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        var result = await service.SendChatMessageAsync(_workspaceA.Id, conv.Id, new SendChatMessageRequest("What is the architecture?"));

        Assert.True(result.IsSuccess);
        Assert.Equal("User", result.Value.UserMessage.Role);
        Assert.Equal("What is the architecture?", result.Value.UserMessage.Content);
        Assert.Equal("Assistant", result.Value.AssistantMessage.Role);
        Assert.Equal("The architecture pattern is modular monolith.", result.Value.AssistantMessage.Content);
        Assert.Single(result.Value.Sources);
        Assert.Equal("Arch.pdf", result.Value.Sources[0].Title);

        // Check database persistence
        var dbMessages = await _context.AiMessages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        Assert.Equal(2, dbMessages.Count);

        var dbSources = await _context.SourceReferences.Where(s => s.AiMessageId == result.Value.AssistantMessage.Id).ToListAsync();
        Assert.Single(dbSources);
        Assert.Equal("Arch.pdf", dbSources[0].SourceTitle);
    }

    [Fact]
    public async Task SendChatMessageAsync_LlmFails_RetainsUserMessageWithoutFakeAssistantMessage()
    {
        var mockRag = new MockRagService { ShouldFail = true };
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "Test Chat" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        var result = await service.SendChatMessageAsync(_workspaceA.Id, conv.Id, new SendChatMessageRequest("Will fail"));

        Assert.False(result.IsSuccess);
        Assert.Equal("AI.ProviderError", result.Error.Code);

        // User message was stored safely
        var dbMessages = await _context.AiMessages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        Assert.Single(dbMessages);
        Assert.Equal(Domain.Enums.AiRole.User, dbMessages[0].Role);
    }

    [Fact]
    public async Task ArchiveAndDeleteConversationAsync_WorksCorrectly()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "To Archive" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        var archiveRes = await service.ArchiveConversationAsync(_workspaceA.Id, conv.Id);
        Assert.True(archiveRes.IsSuccess);

        var archived = await _context.AiConversations.FindAsync(conv.Id);
        Assert.True(archived!.IsArchived);

        var deleteRes = await service.DeleteConversationAsync(_workspaceA.Id, conv.Id);
        Assert.True(deleteRes.IsSuccess);

        var deleted = await _context.AiConversations.FindAsync(conv.Id);
        Assert.True(deleted!.IsDeleted);
    }
}
