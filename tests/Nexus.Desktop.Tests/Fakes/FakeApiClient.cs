using System.IO;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Search;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Study;
using Nexus.Desktop.Services;
using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Desktop.Tests.Fakes;

public class FakeApiClient : IApiClient
{
    public List<PageTreeNodeDto> PageTrees { get; set; } = new();
    public List<PageDto> Pages { get; set; } = new();
    public List<NoteSummaryDto> NoteSummaries { get; set; } = new();
    public List<NoteDto> Notes { get; set; } = new();
    public List<WorkspaceSummaryDto> Workspaces { get; set; } = new();
    public List<StudyTopicDto> StudyTopics { get; set; } = new();
    public List<StudySessionDto> StudySessions { get; set; } = new();
    public List<FlashcardDto> Flashcards { get; set; } = new();
    public List<QuizDto> Quizzes { get; set; } = new();
    public List<QuizDetailDto> QuizDetails { get; set; } = new();
    public List<QuizAttemptResultDto> QuizAttempts { get; set; } = new();

    public bool ShouldFail { get; set; }
    public Error FailureError { get; set; } = new("Test.Error", "Simulated failure");

    public Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AuthResponse>(FailureError));
        return Task.FromResult(Result.Success(new AuthResponse(Guid.NewGuid(), request.Email, request.FullName, "User", "fake-token", DateTime.UtcNow.AddDays(1))));
    }

    public Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AuthResponse>(FailureError));
        return Task.FromResult(Result.Success(new AuthResponse(Guid.NewGuid(), request.Email, "Test User", "User", "fake-token", DateTime.UtcNow.AddDays(1))));
    }

    public Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<UserDto>(FailureError));
        return Task.FromResult(Result.Success(new UserDto(Guid.NewGuid(), "test@nexus.ai", "Test User", "User", null, DateTime.UtcNow)));
    }

    public Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(FailureError));
        return Task.FromResult(Result.Success<IReadOnlyList<WorkspaceSummaryDto>>(Workspaces));
    }

    public Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<WorkspaceDto>(FailureError));
        return Task.FromResult(Result.Success(new WorkspaceDto(workspaceId, "Test Workspace", "Desc", "📁", "#3B82F6", Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)));
    }

    public Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<WorkspaceDto>(FailureError));
        return Task.FromResult(Result.Success(new WorkspaceDto(Guid.NewGuid(), request.Name, request.Description, request.Icon, request.ColorHex, Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)));
    }

    public Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<WorkspaceDto>(FailureError));
        return Task.FromResult(Result.Success(new WorkspaceDto(workspaceId, request.Name, request.Description, request.Icon, request.ColorHex, Guid.NewGuid(), "Owner", DateTime.UtcNow, 0, 0, 0, 0)));
    }

    public Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        return Task.FromResult(Result.Success());
    }

    // Pages
    public Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<PageTreeNodeDto>>(FailureError));
        return Task.FromResult(Result.Success<IReadOnlyList<PageTreeNodeDto>>(PageTrees));
    }

    public Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var page = Pages.FirstOrDefault(p => p.Id == pageId);
        if (page == null) return Task.FromResult(Result.Failure<PageDto>(new Error("Pages.NotFound", "Page not found.")));
        return Task.FromResult(Result.Success(page));
    }

    public Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var newPage = new PageDto(
            Guid.NewGuid(),
            workspaceId,
            request.ParentPageId,
            request.Title,
            request.Icon,
            request.CoverImageUrl,
            request.ContentJson,
            request.OrderIndex,
            DateTime.UtcNow,
            null,
            0,
            0);
        Pages.Add(newPage);
        PageTrees.Add(new PageTreeNodeDto(newPage.Id, newPage.WorkspaceId, newPage.ParentPageId, newPage.Title, newPage.Icon, newPage.OrderIndex, new List<PageTreeNodeDto>()));
        return Task.FromResult(Result.Success(newPage));
    }

    public Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var page = Pages.FirstOrDefault(p => p.Id == pageId);
        var updated = new PageDto(
            pageId,
            workspaceId,
            page?.ParentPageId,
            request.Title,
            request.Icon,
            request.CoverImageUrl,
            request.ContentJson,
            request.OrderIndex,
            DateTime.UtcNow,
            DateTime.UtcNow,
            page?.ChildPagesCount ?? 0,
            page?.NotesCount ?? 0);
        Pages.RemoveAll(p => p.Id == pageId);
        Pages.Add(updated);
        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PageDto>(FailureError));
        var page = Pages.FirstOrDefault(p => p.Id == pageId);
        var moved = new PageDto(
            pageId,
            workspaceId,
            request.TargetParentPageId,
            page?.Title ?? "Page",
            page?.Icon ?? "📄",
            page?.CoverImageUrl,
            page?.ContentJson ?? "{}",
            request.NewOrderIndex,
            DateTime.UtcNow,
            DateTime.UtcNow,
            page?.ChildPagesCount ?? 0,
            page?.NotesCount ?? 0);
        Pages.RemoveAll(p => p.Id == pageId);
        Pages.Add(moved);
        return Task.FromResult(Result.Success(moved));
    }

    public Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        Pages.RemoveAll(p => p.Id == pageId);
        PageTrees.RemoveAll(p => p.Id == pageId);
        return Task.FromResult(Result.Success());
    }

    // Notes
    public Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<NoteSummaryDto>>(FailureError));
        var query = NoteSummaries.AsEnumerable();
        if (pageId.HasValue)
        {
            query = query.Where(n => n.PageId == pageId.Value);
        }
        if (isPinned.HasValue)
        {
            query = query.Where(n => n.IsPinned == isPinned.Value);
        }
        return Task.FromResult(Result.Success<IReadOnlyList<NoteSummaryDto>>(query.ToList()));
    }

    public Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var note = Notes.FirstOrDefault(n => n.Id == noteId);
        if (note == null) return Task.FromResult(Result.Failure<NoteDto>(new Error("Notes.NotFound", "Note not found.")));
        return Task.FromResult(Result.Success(note));
    }

    public Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var tags = request.Tags ?? new List<string>();
        var newNote = new NoteDto(
            Guid.NewGuid(),
            workspaceId,
            request.PageId,
            null,
            request.Title,
            request.Content,
            request.ContentType,
            request.IsPinned,
            DateTime.UtcNow,
            null,
            tags);
        Notes.Add(newNote);
        NoteSummaries.Add(new NoteSummaryDto(
            newNote.Id,
            newNote.WorkspaceId,
            newNote.PageId,
            null,
            newNote.Title,
            newNote.Content.Length > 50 ? newNote.Content[..50] : newNote.Content,
            newNote.ContentType,
            newNote.IsPinned,
            newNote.CreatedAtUtc,
            null,
            tags));
        return Task.FromResult(Result.Success(newNote));
    }

    public Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var tags = request.Tags ?? new List<string>();
        var updated = new NoteDto(
            noteId,
            workspaceId,
            request.PageId,
            null,
            request.Title,
            request.Content,
            request.ContentType,
            request.IsPinned,
            DateTime.UtcNow,
            DateTime.UtcNow,
            tags);
        Notes.RemoveAll(n => n.Id == noteId);
        Notes.Add(updated);

        NoteSummaries.RemoveAll(n => n.Id == noteId);
        NoteSummaries.Add(new NoteSummaryDto(
            noteId,
            workspaceId,
            request.PageId,
            null,
            request.Title,
            request.Content.Length > 50 ? request.Content[..50] : request.Content,
            request.ContentType,
            request.IsPinned,
            DateTime.UtcNow,
            DateTime.UtcNow,
            tags));

        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        Notes.RemoveAll(n => n.Id == noteId);
        NoteSummaries.RemoveAll(n => n.Id == noteId);
        return Task.FromResult(Result.Success());
    }

    // Documents
    public List<Nexus.Application.DTOs.Documents.DocumentSummaryDto> DocumentSummaries { get; set; } = new();
    public List<Nexus.Application.DTOs.Documents.DocumentDetailDto> DocumentDetails { get; set; } = new();

    public Task<Result<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentSummaryDto>>> GetDocumentsAsync(Guid workspaceId, Guid? pageId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentSummaryDto>>(FailureError));
        var query = DocumentSummaries.Where(d => d.WorkspaceId == workspaceId);
        if (pageId.HasValue) query = query.Where(d => d.PageId == pageId.Value);
        return Task.FromResult(Result.Success<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentSummaryDto>>(query.ToList()));
    }

    public Task<Result<Nexus.Application.DTOs.Documents.DocumentDetailDto>> GetDocumentByIdAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.DocumentDetailDto>(FailureError));
        var doc = DocumentDetails.FirstOrDefault(d => d.Id == documentId && d.WorkspaceId == workspaceId);
        if (doc == null) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.DocumentDetailDto>(Error.NotFound));
        return Task.FromResult(Result.Success(doc));
    }

    public Task<Result<Nexus.Application.DTOs.Documents.DocumentDto>> UploadDocumentAsync(
        Guid workspaceId,
        Stream fileStream,
        string fileName,
        string contentType,
        string? title = null,
        Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.DocumentDto>(FailureError));
        var id = Guid.NewGuid();
        var docDto = new Nexus.Application.DTOs.Documents.DocumentDto(
            id,
            workspaceId,
            pageId,
            null,
            title ?? fileName,
            fileName,
            contentType,
            Path.GetExtension(fileName).ToLowerInvariant(),
            fileStream.Length,
            Nexus.Domain.Enums.DocumentStatus.Processed,
            null,
            1,
            100,
            DateTime.UtcNow,
            null);

        DocumentSummaries.Add(new Nexus.Application.DTOs.Documents.DocumentSummaryDto(
            id,
            workspaceId,
            pageId,
            null,
            title ?? fileName,
            fileName,
            contentType,
            Path.GetExtension(fileName).ToLowerInvariant(),
            fileStream.Length,
            Nexus.Domain.Enums.DocumentStatus.Processed,
            DateTime.UtcNow));

        DocumentDetails.Add(new Nexus.Application.DTOs.Documents.DocumentDetailDto(
            id,
            workspaceId,
            pageId,
            null,
            title ?? fileName,
            fileName,
            contentType,
            Path.GetExtension(fileName).ToLowerInvariant(),
            fileStream.Length,
            Nexus.Domain.Enums.DocumentStatus.Processed,
            null,
            "Sample extracted text from document",
            1,
            34,
            DateTime.UtcNow,
            null));

        return Task.FromResult(Result.Success(docDto));
    }

    public Task<Result> DeleteDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure(FailureError));
        DocumentSummaries.RemoveAll(d => d.Id == documentId);
        DocumentDetails.RemoveAll(d => d.Id == documentId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<byte[]>> DownloadDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<byte[]>(FailureError));
        return Task.FromResult(Result.Success(new byte[] { 1, 2, 3, 4 }));
    }

    public List<Nexus.Application.DTOs.Documents.DocumentChunkDto> DocumentChunks { get; set; } = new();

    public Task<Result<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentChunkDto>>> ChunkDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentChunkDto>>(FailureError));
        var chunks = DocumentChunks.Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentChunkDto>>(chunks));
    }

    public Task<Result<Nexus.Application.DTOs.Documents.GenerateEmbeddingsResponse>> EmbedDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Documents.GenerateEmbeddingsResponse>(FailureError));
        return Task.FromResult(Result.Success(new Nexus.Application.DTOs.Documents.GenerateEmbeddingsResponse(documentId, DocumentChunks.Count)));
    }

    public Task<Result<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentChunkDto>>> GetDocumentChunksAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentChunkDto>>(FailureError));
        var chunks = DocumentChunks.Where(c => c.DocumentId == documentId && c.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<Nexus.Application.DTOs.Documents.DocumentChunkDto>>(chunks));
    }

    // Search
    public List<SearchResultDto> SearchResults { get; set; } = new();

    public Task<Result<PagedResult<SearchResultDto>>> SearchAsync(Guid workspaceId, SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<PagedResult<SearchResultDto>>(FailureError));

        var query = SearchResults.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.Type) && !request.Type.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.Type.Equals(request.Type, StringComparison.OrdinalIgnoreCase));
        }

        var items = query.ToList();
        var paged = new PagedResult<SearchResultDto>(items, request.Page, request.PageSize, items.Count);
        return Task.FromResult(Result.Success(paged));
    }

    // AI Conversations & Chat
    public List<Nexus.Application.DTOs.Conversations.ConversationDto> Conversations { get; set; } = new();
    public List<Nexus.Application.DTOs.Conversations.ChatMessageDto> Messages { get; set; } = new();

    public Task<Result<IReadOnlyList<Nexus.Application.DTOs.Conversations.ConversationDto>>> GetConversationsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<Nexus.Application.DTOs.Conversations.ConversationDto>>(FailureError));
        var list = Conversations.Where(c => c.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<Nexus.Application.DTOs.Conversations.ConversationDto>>(list));
    }

    public Task<Result<Nexus.Application.DTOs.Conversations.ConversationDto>> CreateConversationAsync(Guid workspaceId, Nexus.Application.DTOs.Conversations.CreateConversationRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Conversations.ConversationDto>(FailureError));
        var conv = new Nexus.Application.DTOs.Conversations.ConversationDto(
            Guid.NewGuid(),
            workspaceId,
            Guid.NewGuid(),
            request?.Title ?? "New Conversation",
            false,
            DateTime.UtcNow,
            null,
            0);
        Conversations.Insert(0, conv);
        return Task.FromResult(Result.Success(conv));
    }

    public Task<Result<Nexus.Application.DTOs.Conversations.ConversationDto>> GetConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Conversations.ConversationDto>(FailureError));
        var conv = Conversations.FirstOrDefault(c => c.Id == conversationId && c.WorkspaceId == workspaceId);
        if (conv == null) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Conversations.ConversationDto>(new Error("Conversation.NotFound", "Not found.")));
        return Task.FromResult(Result.Success(conv));
    }

    public Task<Result<IReadOnlyList<Nexus.Application.DTOs.Conversations.ChatMessageDto>>> GetConversationMessagesAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<Nexus.Application.DTOs.Conversations.ChatMessageDto>>(FailureError));
        var msgs = Messages.Where(m => m.ConversationId == conversationId).ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<Nexus.Application.DTOs.Conversations.ChatMessageDto>>(msgs));
    }

    public Task<Result<Nexus.Application.DTOs.Conversations.SendChatMessageResponse>> SendChatMessageAsync(Guid workspaceId, Guid conversationId, Nexus.Application.DTOs.Conversations.SendChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<Nexus.Application.DTOs.Conversations.SendChatMessageResponse>(FailureError));
        var userMsg = new Nexus.Application.DTOs.Conversations.ChatMessageDto(
            Guid.NewGuid(),
            conversationId,
            "User",
            request.Content,
            DateTime.UtcNow,
            null,
            Array.Empty<Nexus.Application.DTOs.Conversations.ChatSourceDto>());
        Messages.Add(userMsg);

        var sources = new List<Nexus.Application.DTOs.Conversations.ChatSourceDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), null, null, null, "Mock Source", "Document", 0.95, 1, "Mock snippet")
        };

        var assistantMsg = new Nexus.Application.DTOs.Conversations.ChatMessageDto(
            Guid.NewGuid(),
            conversationId,
            "Assistant",
            "This is a mock assistant answer for: " + request.Content,
            DateTime.UtcNow,
            50,
            sources);
        Messages.Add(assistantMsg);

        var conv = Conversations.FirstOrDefault(c => c.Id == conversationId) ?? new Nexus.Application.DTOs.Conversations.ConversationDto(
            conversationId, workspaceId, Guid.NewGuid(), "Chat", false, DateTime.UtcNow, DateTime.UtcNow, Messages.Count);

        return Task.FromResult(Result.Success(new Nexus.Application.DTOs.Conversations.SendChatMessageResponse(conv, userMsg, assistantMsg, sources)));
    }

    public Task<Result<bool>> ArchiveConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<bool>(FailureError));
        var conv = Conversations.FirstOrDefault(c => c.Id == conversationId);
        if (conv != null)
        {
            Conversations.Remove(conv);
            Conversations.Add(conv with { IsArchived = true });
        }
        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<bool>> DeleteConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<bool>(FailureError));
        Conversations.RemoveAll(c => c.Id == conversationId);
        return Task.FromResult(Result.Success(true));
    }

    // Knowledge Intelligence & AI Workflows
    public List<AiOperationResultDto> AiGenerations { get; set; } = new();

    public Task<Result<AiOperationResultDto>> SummarizeAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AiOperationResultDto>(FailureError));
        var result = new AiOperationResultDto(
            Guid.NewGuid(),
            "Summarize",
            "Summary result for selected sources.",
            null,
            null,
            new List<ChatSourceDto> { new(Guid.NewGuid(), Guid.NewGuid(), null, null, null, "Source 1", "Document", 0.9, 1, "Snippet") },
            "gpt-4o-mini",
            DateTime.UtcNow);
        AiGenerations.Insert(0, result);
        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<AiOperationResultDto>> ExplainAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AiOperationResultDto>(FailureError));
        var result = new AiOperationResultDto(
            Guid.NewGuid(),
            "Explain",
            "Explanation: " + (request.AdditionalInstructions ?? "General concept explanation."),
            null,
            null,
            new List<ChatSourceDto>(),
            "gpt-4o-mini",
            DateTime.UtcNow);
        AiGenerations.Insert(0, result);
        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<AiOperationResultDto>> ExtractKeyPointsAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AiOperationResultDto>(FailureError));
        var result = new AiOperationResultDto(
            Guid.NewGuid(),
            "KeyPoints",
            "• Key point 1\n• Key point 2\n• Key point 3",
            new List<string> { "Key point 1", "Key point 2", "Key point 3" },
            null,
            new List<ChatSourceDto>(),
            "gpt-4o-mini",
            DateTime.UtcNow);
        AiGenerations.Insert(0, result);
        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<AiOperationResultDto>> GenerateQuestionsAsync(Guid workspaceId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AiOperationResultDto>(FailureError));
        var questions = new List<GeneratedQuestionDto>
        {
            new(Guid.NewGuid(), "What is the core idea?", "Intermediate", "Explanation for A", new List<ChatSourceDto>())
        };
        var result = new AiOperationResultDto(
            Guid.NewGuid(),
            "Questions",
            "Generated questions.",
            null,
            questions,
            new List<ChatSourceDto>(),
            "gpt-4o-mini",
            DateTime.UtcNow);
        AiGenerations.Insert(0, result);
        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<AiOperationResultDto>> GenerateStudyMaterialAsync(Guid workspaceId, GenerateStudyMaterialRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AiOperationResultDto>(FailureError));
        var result = new AiOperationResultDto(
            Guid.NewGuid(),
            "StudyMaterial",
            "# Study Guide\n\nStudy material content.",
            null,
            null,
            new List<ChatSourceDto>(),
            "gpt-4o-mini",
            DateTime.UtcNow);
        AiGenerations.Insert(0, result);
        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<NoteDto>> SaveAiOutputAsNoteAsync(Guid workspaceId, SaveAiOutputAsNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<NoteDto>(FailureError));
        var gen = AiGenerations.FirstOrDefault(g => g.Id == request.AiGenerationId);
        var content = gen?.Content ?? "Generated content";
        var note = new NoteDto(
            Guid.NewGuid(),
            workspaceId,
            request.PageId,
            null,
            request.Title,
            content,
            "markdown",
            false,
            DateTime.UtcNow,
            null,
            new List<string> { "ai-generated" });
        Notes.Add(note);
        NoteSummaries.Add(new NoteSummaryDto(
            note.Id, workspaceId, note.PageId, null, note.Title,
            note.Content.Length > 50 ? note.Content[..50] : note.Content,
            note.ContentType, false, note.CreatedAtUtc, null, note.Tags));
        return Task.FromResult(Result.Success(note));
    }

    public Task<Result<IReadOnlyList<AiGenerationSummaryDto>>> GetAiGenerationsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<AiGenerationSummaryDto>>(FailureError));
        var summaries = AiGenerations
            .Select(g => new AiGenerationSummaryDto(
                g.Id,
                g.Operation,
                g.Content.Length > 80 ? g.Content[..80] : g.Content,
                "Document",
                Guid.NewGuid(),
                g.Model,
                g.CreatedAtUtc))
            .ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<AiGenerationSummaryDto>>(summaries));
    }

    public Task<Result<AiOperationResultDto>> GetAiGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<AiOperationResultDto>(FailureError));
        var gen = AiGenerations.FirstOrDefault(g => g.Id == generationId);
        if (gen == null) return Task.FromResult(Result.Failure<AiOperationResultDto>(new Error("AiGeneration.NotFound", "Not found.")));
        return Task.FromResult(Result.Success(gen));
    }

    public Task<Result<bool>> DeleteAiGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<bool>(FailureError));
        AiGenerations.RemoveAll(g => g.Id == generationId);
        return Task.FromResult(Result.Success(true));
    }

    #region Study Engine & AI Tutor (Phase 6)

    public Task<Result<IReadOnlyList<StudyTopicDto>>> GetTopicsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<StudyTopicDto>>(FailureError));
        return Task.FromResult(Result.Success<IReadOnlyList<StudyTopicDto>>(StudyTopics));
    }

    public Task<Result<StudyTopicDto>> CreateTopicAsync(Guid workspaceId, CreateStudyTopicRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<StudyTopicDto>(FailureError));
        var topic = new StudyTopicDto(Guid.NewGuid(), workspaceId, Guid.NewGuid(), request.Title, request.Description, request.SourceDocumentId, request.SourcePageId, request.SourceNoteId, 0, 0, DateTime.UtcNow, null);
        StudyTopics.Add(topic);
        return Task.FromResult(Result.Success(topic));
    }

    public Task<Result<StudyTopicDto>> UpdateTopicAsync(Guid workspaceId, Guid topicId, UpdateStudyTopicRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<StudyTopicDto>(FailureError));
        var existing = StudyTopics.FirstOrDefault(t => t.Id == topicId);
        if (existing == null) return Task.FromResult(Result.Failure<StudyTopicDto>(new Error("Topic.NotFound", "Not found.")));
        var updated = existing with { Title = request.Title, Description = request.Description, UpdatedAtUtc = DateTime.UtcNow };
        StudyTopics.Remove(existing);
        StudyTopics.Add(updated);
        return Task.FromResult(Result.Success(updated));
    }

    public Task<Result<bool>> DeleteTopicAsync(Guid workspaceId, Guid topicId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<bool>(FailureError));
        StudyTopics.RemoveAll(t => t.Id == topicId);
        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<IReadOnlyList<StudySessionDto>>> GetStudySessionsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<StudySessionDto>>(FailureError));
        var list = topicId.HasValue ? StudySessions.Where(s => s.StudyTopicId == topicId.Value).ToList() : StudySessions;
        return Task.FromResult(Result.Success<IReadOnlyList<StudySessionDto>>(list));
    }

    public Task<Result<StudySessionDto>> StartStudySessionAsync(Guid workspaceId, Guid? topicId, StartStudySessionRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<StudySessionDto>(FailureError));
        var session = new StudySessionDto(Guid.NewGuid(), workspaceId, Guid.NewGuid(), topicId, request.Title ?? "Session", DateTime.UtcNow, null, null, 0, StudySessionStatus.InProgress, 0, 0, request.Notes);
        StudySessions.Add(session);
        return Task.FromResult(Result.Success(session));
    }

    public Task<Result<StudySessionDto>> CompleteStudySessionAsync(Guid workspaceId, Guid sessionId, CompleteStudySessionRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<StudySessionDto>(FailureError));
        var existing = StudySessions.FirstOrDefault(s => s.Id == sessionId);
        var completed = existing != null
            ? existing with { Status = StudySessionStatus.Completed, CompletedAtUtc = DateTime.UtcNow, DurationMinutes = request.DurationMinutes, ItemsAttempted = request.ItemsAttempted, ItemsCompleted = request.ItemsCompleted }
            : new StudySessionDto(sessionId, workspaceId, Guid.NewGuid(), null, "Session", DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, DateTime.UtcNow, request.DurationMinutes, StudySessionStatus.Completed, request.ItemsAttempted, request.ItemsCompleted, request.Notes);
        return Task.FromResult(Result.Success(completed));
    }

    public Task<Result<IReadOnlyList<FlashcardDto>>> GetFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<FlashcardDto>>(FailureError));
        var list = topicId.HasValue ? Flashcards.Where(f => f.StudyTopicId == topicId.Value).ToList() : Flashcards;
        return Task.FromResult(Result.Success<IReadOnlyList<FlashcardDto>>(list));
    }

    public Task<Result<IReadOnlyList<FlashcardDto>>> GetDueFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<FlashcardDto>>(FailureError));
        var now = DateTime.UtcNow;
        var list = Flashcards.Where(f => (!topicId.HasValue || f.StudyTopicId == topicId.Value) && f.NextReviewAtUtc <= now).ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<FlashcardDto>>(list));
    }

    public Task<Result<FlashcardDto>> CreateFlashcardAsync(Guid workspaceId, Guid? topicId, CreateFlashcardRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<FlashcardDto>(FailureError));
        var card = new FlashcardDto(Guid.NewGuid(), workspaceId, Guid.NewGuid(), topicId, null, request.FrontText, request.BackText, 2.5, 0, 1, DateTime.UtcNow, 0, 0, 0, null, request.Difficulty, FlashcardState.New, request.SourceDocumentId, null, request.SourcePageId, request.SourceNoteId, null, DateTime.UtcNow);
        Flashcards.Add(card);
        return Task.FromResult(Result.Success(card));
    }

    public Task<Result<FlashcardDto>> ReviewFlashcardAsync(Guid workspaceId, Guid flashcardId, ReviewFlashcardRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<FlashcardDto>(FailureError));
        var card = Flashcards.FirstOrDefault(f => f.Id == flashcardId);
        if (card == null)
        {
            card = new FlashcardDto(flashcardId, workspaceId, Guid.NewGuid(), null, null, "Front", "Back", 2.5, 1, 1, DateTime.UtcNow.AddDays(1), 1, 1, 0, DateTime.UtcNow, "Medium", FlashcardState.Review, null, null, null, null, null, DateTime.UtcNow);
        }
        else
        {
            card = card with { ReviewCount = card.ReviewCount + 1, CorrectCount = card.CorrectCount + 1, Repetitions = card.Repetitions + 1, NextReviewAtUtc = DateTime.UtcNow.AddDays(1) };
        }
        return Task.FromResult(Result.Success(card));
    }

    public Task<Result<IReadOnlyList<FlashcardDto>>> GenerateFlashcardsAsync(Guid workspaceId, Guid? topicId, GenerateFlashcardsRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<FlashcardDto>>(FailureError));
        var generated = new List<FlashcardDto>();
        for (int i = 0; i < request.Count; i++)
        {
            var card = new FlashcardDto(Guid.NewGuid(), workspaceId, Guid.NewGuid(), topicId, null, $"Generated Front {i + 1}", $"Generated Back {i + 1}", 2.5, 0, 1, DateTime.UtcNow, 0, 0, 0, null, request.Difficulty, FlashcardState.New, null, null, null, null, null, DateTime.UtcNow);
            Flashcards.Add(card);
            generated.Add(card);
        }
        return Task.FromResult(Result.Success<IReadOnlyList<FlashcardDto>>(generated));
    }

    public Task<Result<bool>> DeleteFlashcardAsync(Guid workspaceId, Guid flashcardId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<bool>(FailureError));
        Flashcards.RemoveAll(f => f.Id == flashcardId);
        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<IReadOnlyList<QuizDto>>> GetQuizzesAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<IReadOnlyList<QuizDto>>(FailureError));
        var list = topicId.HasValue ? Quizzes.Where(q => q.StudyTopicId == topicId.Value).ToList() : Quizzes;
        return Task.FromResult(Result.Success<IReadOnlyList<QuizDto>>(list));
    }

    public Task<Result<SafeQuizDetailDto>> GetSafeQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<SafeQuizDetailDto>(FailureError));
        var quiz = QuizDetails.FirstOrDefault(q => q.Id == quizId);
        if (quiz != null)
        {
            var safeQns = quiz.Questions.Select(qn => new SafeQuizQuestionDto(qn.Id, qn.QuizId, qn.QuestionText, qn.QuestionType, qn.Options, qn.Difficulty, qn.OrderIndex)).ToList();
            return Task.FromResult(Result.Success(new SafeQuizDetailDto(quiz.Id, quiz.WorkspaceId, quiz.UserId, quiz.StudyTopicId, quiz.Title, quiz.Description, quiz.DifficultyLevel, safeQns, quiz.CreatedAtUtc)));
        }
        var dummySafe = new SafeQuizDetailDto(quizId, workspaceId, Guid.NewGuid(), null, "Test Quiz", "Desc", "Medium", new List<SafeQuizQuestionDto>
        {
            new(Guid.NewGuid(), quizId, "Question 1", "MultipleChoice", new[] { "Option A", "Option B" }, "Medium", 0)
        }, DateTime.UtcNow);
        return Task.FromResult(Result.Success(dummySafe));
    }

    public Task<Result<QuizDetailDto>> GetQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<QuizDetailDto>(FailureError));
        var quiz = QuizDetails.FirstOrDefault(q => q.Id == quizId);
        if (quiz != null) return Task.FromResult(Result.Success(quiz));
        var dummy = new QuizDetailDto(quizId, workspaceId, Guid.NewGuid(), null, "Test Quiz", "Desc", "Medium", new List<QuizQuestionDto>
        {
            new(Guid.NewGuid(), quizId, "Question 1", "MultipleChoice", new[] { "Option A", "Option B" }, "Option A", "Expl", "Medium", 0, null, null, null)
        }, DateTime.UtcNow);
        return Task.FromResult(Result.Success(dummy));
    }

    public Task<Result<QuizDetailDto>> GenerateQuizAsync(Guid workspaceId, Guid? topicId, GenerateQuizRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<QuizDetailDto>(FailureError));
        var quizId = Guid.NewGuid();
        var questions = new List<QuizQuestionDto>
        {
            new(Guid.NewGuid(), quizId, "Question 1", "MultipleChoice", new[] { "Option A", "Option B" }, "Option A", "Explanation 1", request.DifficultyLevel, 0, null, null, null)
        };
        var quiz = new QuizDetailDto(quizId, workspaceId, Guid.NewGuid(), topicId, "Generated Quiz", "Desc", request.DifficultyLevel, questions, DateTime.UtcNow);
        QuizDetails.Add(quiz);
        Quizzes.Add(new QuizDto(quizId, workspaceId, Guid.NewGuid(), topicId, "Generated Quiz", "Desc", request.DifficultyLevel, questions.Count, DateTime.UtcNow));
        return Task.FromResult(Result.Success(quiz));
    }

    public Task<Result<QuizAttemptResultDto>> StartQuizAttemptAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<QuizAttemptResultDto>(FailureError));
        var attempt = new QuizAttemptResultDto(Guid.NewGuid(), quizId, Guid.NewGuid(), workspaceId, 0, 0, 1, 0, false, DateTime.UtcNow, DateTime.UtcNow, Array.Empty<QuizAnswerResultDto>(), null);
        QuizAttempts.Add(attempt);
        return Task.FromResult(Result.Success(attempt));
    }

    public Task<Result<QuizAttemptResultDto>> SubmitQuizAttemptAsync(Guid workspaceId, Guid attemptId, SubmitQuizAttemptRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<QuizAttemptResultDto>(FailureError));
        var answers = request.Answers.Select(a => new QuizAnswerResultDto(a.QuestionId, "QText", a.SubmittedAnswer, "Option A", a.SubmittedAnswer == "Option A", "Expl")).ToList();
        int correct = answers.Count(a => a.IsCorrect);
        double pct = answers.Count > 0 ? ((double)correct / answers.Count) * 100 : 0;
        var attempt = new QuizAttemptResultDto(attemptId, Guid.NewGuid(), Guid.NewGuid(), workspaceId, pct, correct, answers.Count, correct, true, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow, answers, "Feedback");
        return Task.FromResult(Result.Success(attempt));
    }

    public Task<Result<QuizAttemptResultDto>> GetQuizAttemptResultAsync(Guid workspaceId, Guid attemptId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<QuizAttemptResultDto>(FailureError));
        var attempt = QuizAttempts.FirstOrDefault(a => a.AttemptId == attemptId)
            ?? new QuizAttemptResultDto(attemptId, Guid.NewGuid(), Guid.NewGuid(), workspaceId, 100, 1, 1, 1, true, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow, Array.Empty<QuizAnswerResultDto>(), null);
        return Task.FromResult(Result.Success(attempt));
    }

    public Task<Result<bool>> DeleteQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<bool>(FailureError));
        Quizzes.RemoveAll(q => q.Id == quizId);
        QuizDetails.RemoveAll(q => q.Id == quizId);
        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<KnowledgeAssessmentDto>> GetAssessmentAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<KnowledgeAssessmentDto>(FailureError));
        var dto = new KnowledgeAssessmentDto(workspaceId, Guid.NewGuid(), topicId, 85.0, 90.0, Flashcards.Count, Flashcards.Count(f => f.NextReviewAtUtc <= DateTime.UtcNow), Quizzes.Count, QuizAttempts.Count, new List<TopicPerformanceDto>
        {
            new(Guid.NewGuid(), "Topic A", 10, 2, 8, 90.0, 2, 2, 85.0, 87.0, "Strong")
        }, Array.Empty<TopicPerformanceDto>(), Array.Empty<TopicPerformanceDto>(), new[] { "Keep reviewing!" });
        return Task.FromResult(Result.Success(dto));
    }

    public Task<Result<StudyDashboardDto>> GetStudyDashboardAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<StudyDashboardDto>(FailureError));
        var dto = new StudyDashboardDto(
            TotalTopics: StudyTopics.Count,
            TotalSessions: StudySessions.Count,
            TotalStudyMinutes: 45,
            TotalFlashcards: Flashcards.Count,
            DueFlashcards: Flashcards.Count(f => f.NextReviewAtUtc <= DateTime.UtcNow),
            TotalQuizzes: Quizzes.Count,
            CompletedAttempts: QuizAttempts.Count,
            AverageQuizScore: 88.0,
            OverallMasteryPercentage: 85.0,
            RecentTopics: Array.Empty<TopicPerformanceDto>(),
            DueFlashcardPreviews: Flashcards.Take(3).ToList()
        );
        return Task.FromResult(Result.Success(dto));
    }

    public Task<Result<TutorResponseDto>> TutorChatAsync(Guid workspaceId, TutorChatRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<TutorResponseDto>(FailureError));
        var dto = new TutorResponseDto(request.ConversationId ?? Guid.NewGuid(), "Hello! I am your AI Study Tutor. Let's explore this concept together.", Array.Empty<ChatSourceDto>(), new[] { "Key concept point" }, new[] { "Would you like an example?" });
        return Task.FromResult(Result.Success(dto));
    }

    public Task<Result<TutorHintDto>> TutorHintAsync(Guid workspaceId, TutorHintRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<TutorHintDto>(FailureError));
        return Task.FromResult(Result.Success(new TutorHintDto("Think about how the core principle operates.", 1)));
    }

    public Task<Result<TutorExplanationDto>> TutorExplainWrongAsync(Guid workspaceId, TutorExplainWrongAnswerRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<TutorExplanationDto>(FailureError));
        return Task.FromResult(Result.Success(new TutorExplanationDto("The answer you chose represents a common misconception.", new[] { "Review key definition", "Practice flashcard" }, Array.Empty<ChatSourceDto>())));
    }

    public Task<Result<TutorMiniExerciseDto>> TutorExerciseAsync(Guid workspaceId, TutorMiniExerciseRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult(Result.Failure<TutorMiniExerciseDto>(FailureError));
        return Task.FromResult(Result.Success(new TutorMiniExerciseDto("What is the primary role of this concept?", "MultipleChoice", new[] { "Option A", "Option B" }, "Option A", "Expl")));
    }

    #endregion
}

