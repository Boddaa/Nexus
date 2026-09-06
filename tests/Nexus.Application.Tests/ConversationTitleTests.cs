using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.Features.AI.Services;
using Nexus.Application.Features.Conversations.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.Application.Tests;

public class ConversationTitleTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly User _testUser;
    private readonly Workspace _workspace;

    private class MockRagService : IRagService
    {
        public Task<Result<RagAnswerResult>> AnswerQuestionAsync(
            Guid workspaceId,
            string question,
            IReadOnlyList<LLMChatMessage>? conversationHistory = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(new RagAnswerResult(
                "Answer: Quantum computing uses qubits.",
                Array.Empty<ChatSourceDto>(),
                50,
                30,
                80)));
        }

        public Task<RagResponse> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private class TitleTestLlmService : ILLMService
    {
        public bool ShouldFail { get; set; }
        public string TitleToReturn { get; set; } = "Quantum Computing Overview";

        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new LlmException("LLM timeout or error during title generation");
            }

            return Task.FromResult(new LLMResponse(TitleToReturn, "gpt-4o-mini", 10, 5, 15));
        }
    }

    public ConversationTitleTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "TitleTests_" + Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(dbOptions, _currentUserService);

        _testUser = new User { Email = "carol@nexus.ai", FullName = "Carol" };
        _context.Users.Add(_testUser);

        _workspace = new Workspace { Name = "Quantum Lab", OwnerId = _testUser.Id };
        _context.Workspaces.Add(_workspace);
        _context.SaveChanges();

        _currentUserService.UserId = _testUser.Id;
        _currentUserService.Email = _testUser.Email;
    }

    [Fact]
    public async Task SendChatMessageAsync_FirstMessage_LlmGeneratesCleanTitle_UpdatesTitle()
    {
        // Arrange
        var conv = new AiConversation
        {
            WorkspaceId = _workspace.Id,
            UserId = _testUser.Id,
            Title = "New Conversation"
        };
        _context.AiConversations.Add(conv);
        _context.SaveChanges();

        var llm = new TitleTestLlmService { TitleToReturn = "\"Quantum Computing 101\"." };
        var service = new ConversationService(
            _context,
            _currentUserService,
            new MockRagService(),
            Options.Create(new RagOptions()),
            NullLogger<ConversationService>.Instance,
            llm,
            Options.Create(new AiKnowledgeOptions { EnableTitleGeneration = true }));

        // Act
        var result = await service.SendChatMessageAsync(
            _workspace.Id,
            conv.Id,
            new SendChatMessageRequest("Explain how qubits maintain superposition"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Quantum Computing 101", result.Value.Conversation.Title);

        var updated = await _context.AiConversations.FindAsync(conv.Id);
        Assert.NotNull(updated);
        Assert.Equal("Quantum Computing 101", updated.Title);
    }

    [Fact]
    public async Task SendChatMessageAsync_LlmThrows_FallbackTitlePreservedWithoutError()
    {
        // Arrange
        var conv = new AiConversation
        {
            WorkspaceId = _workspace.Id,
            UserId = _testUser.Id,
            Title = "New Conversation"
        };
        _context.AiConversations.Add(conv);
        _context.SaveChanges();

        var llm = new TitleTestLlmService { ShouldFail = true };
        var service = new ConversationService(
            _context,
            _currentUserService,
            new MockRagService(),
            Options.Create(new RagOptions()),
            NullLogger<ConversationService>.Instance,
            llm,
            Options.Create(new AiKnowledgeOptions { EnableTitleGeneration = true }));

        var prompt = "What are superconducting circuits?";

        // Act
        var result = await service.SendChatMessageAsync(
            _workspace.Id,
            conv.Id,
            new SendChatMessageRequest(prompt));

        // Assert: Chat completes successfully without throwing
        Assert.True(result.IsSuccess);
        // Fallback title was used
        Assert.Equal(prompt, result.Value.Conversation.Title);
    }

    [Fact]
    public async Task SendChatMessageAsync_AlreadyHasCustomTitle_DoesNotOverwriteTitle()
    {
        // Arrange
        var customTitle = "My Custom Research Thread";
        var conv = new AiConversation
        {
            WorkspaceId = _workspace.Id,
            UserId = _testUser.Id,
            Title = customTitle
        };
        _context.AiConversations.Add(conv);
        _context.SaveChanges();

        var llm = new TitleTestLlmService { TitleToReturn = "Ignored LLM Title" };
        var service = new ConversationService(
            _context,
            _currentUserService,
            new MockRagService(),
            Options.Create(new RagOptions()),
            NullLogger<ConversationService>.Instance,
            llm,
            Options.Create(new AiKnowledgeOptions { EnableTitleGeneration = true }));

        // Act
        var result = await service.SendChatMessageAsync(
            _workspace.Id,
            conv.Id,
            new SendChatMessageRequest("A new question"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(customTitle, result.Value.Conversation.Title);
    }
}
