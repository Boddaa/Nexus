using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Search;
using Nexus.Application.Features.AI.Services;
using Nexus.Application.Features.Conversations.Services;
using Nexus.Application.Features.Search.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;
using SearchRequest = Nexus.Application.DTOs.Search.SearchRequest;

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

    private class FakeIntegrationLlmService : ILLMService
    {
        public bool ShouldFail { get; set; } = false;
        public LLMRequest? LastRequest { get; set; }
        public string ResponseContent { get; set; } = "Based on [SOURCE 1], the answer is confirmed.";

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldFail) throw new LlmException("Provider down");
            LastRequest = request;
            return Task.FromResult(new LLMResponse(ResponseContent, "fake-model", 40, 20, 60));
        }
    }

    private class FakeRagSearchService : ISearchService
    {
        public List<SearchResultDto> Items { get; set; } = new();

        public Task<Result<PagedResult<SearchResultDto>>> SearchAsync(Guid workspaceId, SearchRequest request, CancellationToken cancellationToken = default)
        {
            var paged = new PagedResult<SearchResultDto>(Items, 1, 20, Items.Count);
            return Task.FromResult(Result.Success(paged));
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

    #region Authorization Tests (Requirement 3)

    [Fact]
    public async Task UserA_AccessesOwnConversation_Allowed()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "Alice Private Chat" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        // Alice accesses her own conversation
        _currentUserService.UserId = _testUser.Id;
        var result = await service.GetConversationAsync(_workspaceA.Id, conv.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(conv.Id, result.Value.Id);
        Assert.Equal("Alice Private Chat", result.Value.Title);
    }

    [Fact]
    public async Task UserB_AccessesUserAConversation_Denied()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "Alice Private Chat" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        // Switch to Bob
        _currentUserService.UserId = _otherUser.Id;
        var result = await service.GetConversationAsync(_workspaceA.Id, conv.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversation.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UserA_AccessesConversationInAnotherWorkspace_Denied()
    {
        var mockRag = new MockRagService();
        var service = new ConversationService(_context, _currentUserService, mockRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "Alice Private Chat" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        // Alice tries to access the conversation using workspace B's ID
        _currentUserService.UserId = _testUser.Id;
        var result = await service.GetConversationAsync(_workspaceB.Id, conv.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conversation.NotFound", result.Error.Code);
    }

    #endregion

    #region Full Send Message & RAG Integration Pipeline (Requirement 4)

    [Fact]
    public async Task FullSendMessage_Integration_Pipeline_SavesUserAndAssistantAndSources()
    {
        var fakeSearch = new FakeRagSearchService
        {
            Items = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", _workspaceA.Id, null, "Compliance Guide", "Data retention is 7 years.", 90.0, DateTime.UtcNow, null, Guid.NewGuid(), "Hybrid", 0.90, 4, 0.90)
            }
        };

        var fakeLlm = new FakeIntegrationLlmService
        {
            ResponseContent = "As stated in [SOURCE 1], retention period is 7 years."
        };

        var realRag = new RagService(
            fakeSearch,
            new ContextBuilder(),
            new PromptBuilder(),
            new CitationValidator(),
            fakeLlm,
            Options.Create(new RagOptions { MinimumRelevanceScore = 0.05 }),
            Options.Create(new LlmOptions()),
            NullLogger<RagService>.Instance);

        var service = new ConversationService(_context, _currentUserService, realRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "New Conversation" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        // Execute full pipeline
        var response = await service.SendChatMessageAsync(_workspaceA.Id, conv.Id, new SendChatMessageRequest("What is data retention?"));

        Assert.True(response.IsSuccess);
        Assert.Equal("What is data retention?", response.Value.UserMessage.Content);
        Assert.Equal("As stated in [SOURCE 1], retention period is 7 years.", response.Value.AssistantMessage.Content);
        Assert.Single(response.Value.Sources);
        Assert.Equal("Compliance Guide", response.Value.Sources[0].Title);

        // Verify User message is persisted
        var userMsgInDb = await _context.AiMessages.FirstOrDefaultAsync(m => m.Id == response.Value.UserMessage.Id);
        Assert.NotNull(userMsgInDb);
        Assert.Equal(AiRole.User, userMsgInDb.Role);
        Assert.Equal("What is data retention?", userMsgInDb.Content);

        // Verify Assistant message is persisted
        var assistantMsgInDb = await _context.AiMessages.FirstOrDefaultAsync(m => m.Id == response.Value.AssistantMessage.Id);
        Assert.NotNull(assistantMsgInDb);
        Assert.Equal(AiRole.Assistant, assistantMsgInDb.Role);

        // Verify Retrieved sources are persisted in SourceReferences
        var sourcesInDb = await _context.SourceReferences.Where(s => s.AiMessageId == assistantMsgInDb.Id).ToListAsync();
        Assert.Single(sourcesInDb);
        Assert.Equal("Compliance Guide", sourcesInDb[0].SourceTitle);
        Assert.Equal(4, sourcesInDb[0].PageNumber);

        // Returned sources match retrieved sources
        Assert.Equal(response.Value.Sources[0].Title, sourcesInDb[0].SourceTitle);
    }

    [Fact]
    public async Task FullSendMessage_LlmFails_UserMessagePersisted_NoFakeAssistantResponse()
    {
        var fakeSearch = new FakeRagSearchService
        {
            Items = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", _workspaceA.Id, null, "Doc", "Snippet", 80.0, DateTime.UtcNow, null, null, "Semantic", 0.80, null, 0.80)
            }
        };

        var fakeLlm = new FakeIntegrationLlmService { ShouldFail = true };

        var realRag = new RagService(
            fakeSearch,
            new ContextBuilder(),
            new PromptBuilder(),
            new CitationValidator(),
            fakeLlm,
            Options.Create(new RagOptions()),
            Options.Create(new LlmOptions()),
            NullLogger<RagService>.Instance);

        var service = new ConversationService(_context, _currentUserService, realRag, Options.Create(new RagOptions()), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "Fail Test" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        var result = await service.SendChatMessageAsync(_workspaceA.Id, conv.Id, new SendChatMessageRequest("Trigger failure"));

        Assert.False(result.IsSuccess);

        // User message is persisted
        var messages = await _context.AiMessages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        Assert.Single(messages);
        Assert.Equal(AiRole.User, messages[0].Role);
        Assert.Equal("Trigger failure", messages[0].Content);

        // No fake assistant message created
        Assert.Empty(await _context.SourceReferences.ToListAsync());
    }

    [Fact]
    public async Task SendChatMessageAsync_ConversationHistoryLimit_OnlyPassesBoundedMessages()
    {
        var fakeSearch = new FakeRagSearchService
        {
            Items = new List<SearchResultDto>
            {
                new(Guid.NewGuid(), "Document", _workspaceA.Id, null, "Doc", "Snippet", 80.0, DateTime.UtcNow, null, null, "Semantic", 0.80, null, 0.80)
            }
        };

        var fakeLlm = new FakeIntegrationLlmService();

        var ragOptions = new RagOptions { MaxConversationMessages = 3 };
        var realRag = new RagService(
            fakeSearch,
            new ContextBuilder(),
            new PromptBuilder(),
            new CitationValidator(),
            fakeLlm,
            Options.Create(ragOptions),
            Options.Create(new LlmOptions()),
            NullLogger<RagService>.Instance);

        var service = new ConversationService(_context, _currentUserService, realRag, Options.Create(ragOptions), NullLogger<ConversationService>.Instance);

        var conv = new AiConversation { WorkspaceId = _workspaceA.Id, UserId = _testUser.Id, Title = "History Chat" };
        _context.AiConversations.Add(conv);
        await _context.SaveChangesAsync();

        // Seed 6 prior messages with sequential timestamps
        var baseTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var seededMessages = new List<AiMessage>();
        for (int i = 1; i <= 6; i++)
        {
            var msg = new AiMessage
            {
                ConversationId = conv.Id,
                Role = i % 2 == 1 ? AiRole.User : AiRole.Assistant,
                Content = $"Message {i}"
            };
            _context.AiMessages.Add(msg);
            seededMessages.Add(msg);
        }
        await _context.SaveChangesAsync();

        // Update timestamps to be distinct and sequential
        for (int i = 0; i < seededMessages.Count; i++)
        {
            seededMessages[i].CreatedAtUtc = baseTime.AddMinutes(i + 1);
        }
        await _context.SaveChangesAsync();

        // Send 7th message
        var res = await service.SendChatMessageAsync(_workspaceA.Id, conv.Id, new SendChatMessageRequest("Latest question"));
        Assert.True(res.IsSuccess);

        Assert.NotNull(fakeLlm.LastRequest);
        // LLMRequest should have exactly 3 history messages + 1 current prompt message = 4 messages
        Assert.Equal(4, fakeLlm.LastRequest.Messages.Count);
        // History messages should be the most recent 3: Message 4, Message 5, Message 6
        Assert.Equal("Message 4", fakeLlm.LastRequest.Messages[0].Content);
        Assert.Equal("Message 5", fakeLlm.LastRequest.Messages[1].Content);
        Assert.Equal("Message 6", fakeLlm.LastRequest.Messages[2].Content);
    }

    #endregion

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
