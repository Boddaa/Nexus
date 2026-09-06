using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Conversations;
using Nexus.Application.DTOs.Documents;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Search;
using Nexus.Application.DTOs.Study;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Common;

namespace Nexus.Desktop.Services;

public interface IApiClient
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    // Pages
    Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default);

    // Notes
    Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default);

    // Documents
    Task<Result<IReadOnlyList<DocumentSummaryDto>>> GetDocumentsAsync(Guid workspaceId, Guid? pageId = null, CancellationToken cancellationToken = default);
    Task<Result<DocumentDetailDto>> GetDocumentByIdAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<DocumentDto>> UploadDocumentAsync(Guid workspaceId, Stream fileStream, string fileName, string contentType, string? title = null, Guid? pageId = null, CancellationToken cancellationToken = default);
    Task<Result> DeleteDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> DownloadDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentChunkDto>>> ChunkDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<GenerateEmbeddingsResponse>> EmbedDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentChunkDto>>> GetDocumentChunksAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default);

    // Search
    Task<Result<PagedResult<SearchResultDto>>> SearchAsync(Guid workspaceId, SearchRequest request, CancellationToken cancellationToken = default);

    // AI Conversations & Chat
    Task<Result<IReadOnlyList<ConversationDto>>> GetConversationsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<ConversationDto>> CreateConversationAsync(Guid workspaceId, CreateConversationRequest request, CancellationToken cancellationToken = default);
    Task<Result<ConversationDto>> GetConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ChatMessageDto>>> GetConversationMessagesAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<Result<SendChatMessageResponse>> SendChatMessageAsync(Guid workspaceId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> ArchiveConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default);

    // Knowledge Intelligence & AI Workflows
    Task<Result<AiOperationResultDto>> SummarizeAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> ExplainAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> ExtractKeyPointsAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> GenerateQuestionsAsync(Guid workspaceId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> GenerateStudyMaterialAsync(Guid workspaceId, GenerateStudyMaterialRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> SaveAiOutputAsNoteAsync(Guid workspaceId, SaveAiOutputAsNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AiGenerationSummaryDto>>> GetAiGenerationsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> GetAiGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteAiGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default);

    // Study Engine & AI Tutor (Phase 6)
    Task<Result<IReadOnlyList<StudyTopicDto>>> GetTopicsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<StudyTopicDto>> CreateTopicAsync(Guid workspaceId, CreateStudyTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<StudyTopicDto>> UpdateTopicAsync(Guid workspaceId, Guid topicId, UpdateStudyTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteTopicAsync(Guid workspaceId, Guid topicId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StudySessionDto>>> GetStudySessionsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<StudySessionDto>> StartStudySessionAsync(Guid workspaceId, Guid? topicId, StartStudySessionRequest request, CancellationToken cancellationToken = default);
    Task<Result<StudySessionDto>> CompleteStudySessionAsync(Guid workspaceId, Guid sessionId, CompleteStudySessionRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FlashcardDto>>> GetFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FlashcardDto>>> GetDueFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<FlashcardDto>> CreateFlashcardAsync(Guid workspaceId, Guid? topicId, CreateFlashcardRequest request, CancellationToken cancellationToken = default);
    Task<Result<FlashcardDto>> ReviewFlashcardAsync(Guid workspaceId, Guid flashcardId, ReviewFlashcardRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FlashcardDto>>> GenerateFlashcardsAsync(Guid workspaceId, Guid? topicId, GenerateFlashcardsRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteFlashcardAsync(Guid workspaceId, Guid flashcardId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<QuizDto>>> GetQuizzesAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<SafeQuizDetailDto>> GetSafeQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
    Task<Result<QuizDetailDto>> GetQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
    Task<Result<QuizDetailDto>> GenerateQuizAsync(Guid workspaceId, Guid? topicId, GenerateQuizRequest request, CancellationToken cancellationToken = default);
    Task<Result<QuizAttemptResultDto>> StartQuizAttemptAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);
    Task<Result<QuizAttemptResultDto>> SubmitQuizAttemptAsync(Guid workspaceId, Guid attemptId, SubmitQuizAttemptRequest request, CancellationToken cancellationToken = default);
    Task<Result<QuizAttemptResultDto>> GetQuizAttemptResultAsync(Guid workspaceId, Guid attemptId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default);

    Task<Result<KnowledgeAssessmentDto>> GetAssessmentAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default);
    Task<Result<StudyDashboardDto>> GetStudyDashboardAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task<Result<TutorResponseDto>> TutorChatAsync(Guid workspaceId, TutorChatRequest request, CancellationToken cancellationToken = default);
    Task<Result<TutorHintDto>> TutorHintAsync(Guid workspaceId, TutorHintRequest request, CancellationToken cancellationToken = default);
    Task<Result<TutorExplanationDto>> TutorExplainWrongAsync(Guid workspaceId, TutorExplainWrongAnswerRequest request, CancellationToken cancellationToken = default);
    Task<Result<TutorMiniExerciseDto>> TutorExerciseAsync(Guid workspaceId, TutorMiniExerciseRequest request, CancellationToken cancellationToken = default);

    // Visual Thinking: Boards
    Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<BoardDetailDto>> GetBoardByIdAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BoardDto>> CreateBoardAsync(Guid workspaceId, CreateBoardRequest request, CancellationToken cancellationToken = default);
    Task<Result<BoardDto>> UpdateBoardAsync(Guid workspaceId, Guid boardId, UpdateBoardRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBoardAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BoardItemDto>>> GetBoardItemsAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BoardItemDto>> CreateBoardItemAsync(Guid workspaceId, Guid boardId, CreateBoardItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<BoardItemDto>> UpdateBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, UpdateBoardItemRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BoardItemDto>>> BatchUpdateBoardItemsAsync(Guid workspaceId, Guid boardId, BatchUpdateBoardItemsRequest request, CancellationToken cancellationToken = default);

    // Visual Thinking: Mind Maps
    Task<Result<IReadOnlyList<MindMapDto>>> GetMindMapsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<MindMapDetailDto>> GetMindMapByIdAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default);
    Task<Result<MindMapDto>> CreateMindMapAsync(Guid workspaceId, CreateMindMapRequest request, CancellationToken cancellationToken = default);
    Task<Result<MindMapDto>> UpdateMindMapAsync(Guid workspaceId, Guid mindMapId, UpdateMindMapRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteMindMapAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default);

    Task<Result<MindMapNodeDto>> CreateMindMapNodeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result<MindMapNodeDto>> UpdateMindMapNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, UpdateMindMapNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteMindMapNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MindMapNodeDto>>> BatchUpdateMindMapNodesAsync(Guid workspaceId, Guid mindMapId, BatchUpdateMindMapNodesRequest request, CancellationToken cancellationToken = default);

    Task<Result<MindMapEdgeDto>> CreateMindMapEdgeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapEdgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<MindMapEdgeDto>> UpdateMindMapEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, UpdateMindMapEdgeRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteMindMapEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, CancellationToken cancellationToken = default);

    Task<Result<LayoutResultDto>> ApplyMindMapLayoutAsync(Guid workspaceId, Guid mindMapId, ApplyLayoutRequest request, bool persist = false, CancellationToken cancellationToken = default);
    Task<Result<NodeKnowledgeContextDto>> GetNodeKnowledgeContextAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default);
    Task<Result<GeneratedMindMapDto>> GenerateMindMapAsync(Guid workspaceId, GenerateMindMapRequest request, CancellationToken cancellationToken = default);
    Task<Result<NodeExplanationDto>> ExplainNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, ExplainNodeRequest request, CancellationToken cancellationToken = default);
    Task<Result<RelatedKnowledgeResultDto>> FindRelatedKnowledgeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, FindRelatedKnowledgeRequest request, CancellationToken cancellationToken = default);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    private void SetAuthorizationHeader()
    {
        var token = _tokenStorage.GetToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    #region Auth & Workspace Methods

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<AuthResponse>(Error.NullValue);
            }

            return await ExtractErrorAsync<AuthResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<AuthResponse>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<AuthResponse>(Error.NullValue);
            }

            return await ExtractErrorAsync<AuthResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<AuthResponse>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync("api/auth/me", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<UserDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<UserDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<UserDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<WorkspaceSummaryDto>>> GetUserWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync("api/workspaces", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkspaceSummaryDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<WorkspaceSummaryDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<WorkspaceSummaryDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<WorkspaceDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<WorkspaceDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkspaceDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/workspaces", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<WorkspaceDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<WorkspaceDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkspaceDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkspaceDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<WorkspaceDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<WorkspaceDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkspaceDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    #region Pages Methods

    public async Task<Result<IReadOnlyList<PageTreeNodeDto>>> GetPageTreeAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/pages", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<PageTreeNodeDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<PageTreeNodeDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<PageTreeNodeDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<PageTreeNodeDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> GetPageByIdAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/pages/{pageId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> CreatePageAsync(Guid workspaceId, CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/pages", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> UpdatePageAsync(Guid workspaceId, Guid pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/pages/{pageId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PageDto>> MovePageAsync(Guid workspaceId, Guid pageId, MovePageRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/pages/{pageId}/move", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PageDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<PageDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PageDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeletePageAsync(Guid workspaceId, Guid pageId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/pages/{pageId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    #region Notes Methods

    public async Task<Result<IReadOnlyList<NoteSummaryDto>>> GetNotesAsync(Guid workspaceId, Guid? pageId = null, bool? isPinned = null, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var queryParams = new List<string>();
            if (pageId.HasValue)
            {
                queryParams.Add($"pageId={pageId.Value}");
            }
            if (isPinned.HasValue)
            {
                queryParams.Add($"isPinned={isPinned.Value.ToString().ToLowerInvariant()}");
            }

            var url = $"api/workspaces/{workspaceId}/notes";
            if (queryParams.Count > 0)
            {
                url += "?" + string.Join("&", queryParams);
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<NoteSummaryDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<NoteSummaryDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<NoteSummaryDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<NoteSummaryDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NoteDto>> GetNoteByIdAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/notes/{noteId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<NoteDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NoteDto>> CreateNoteAsync(Guid workspaceId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/notes", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<NoteDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NoteDto>> UpdateNoteAsync(Guid workspaceId, Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/notes/{noteId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<NoteDto>(Error.NullValue);
            }

            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteNoteAsync(Guid workspaceId, Guid noteId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/notes/{noteId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    #region Documents

    public async Task<Result<IReadOnlyList<DocumentSummaryDto>>> GetDocumentsAsync(Guid workspaceId, Guid? pageId = null, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var uri = $"api/workspaces/{workspaceId}/documents";
            if (pageId.HasValue)
            {
                uri += $"?pageId={pageId.Value}";
            }

            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var documents = await response.Content.ReadFromJsonAsync<List<DocumentSummaryDto>>(_jsonOptions, cancellationToken);
                return Result.Success<IReadOnlyList<DocumentSummaryDto>>(documents ?? new List<DocumentSummaryDto>());
            }

            return await ExtractErrorAsync<IReadOnlyList<DocumentSummaryDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<DocumentSummaryDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<DocumentDetailDto>> GetDocumentByIdAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/documents/{documentId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var document = await response.Content.ReadFromJsonAsync<DocumentDetailDto>(_jsonOptions, cancellationToken);
                return Result.Success(document!);
            }

            return await ExtractErrorAsync<DocumentDetailDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<DocumentDetailDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<DocumentDto>> UploadDocumentAsync(
        Guid workspaceId,
        Stream fileStream,
        string fileName,
        string contentType,
        string? title = null,
        Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            using var formData = new MultipartFormDataContent();

            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            formData.Add(streamContent, "file", fileName);

            if (!string.IsNullOrWhiteSpace(title))
            {
                formData.Add(new StringContent(title), "title");
            }

            if (pageId.HasValue)
            {
                formData.Add(new StringContent(pageId.Value.ToString()), "pageId");
            }

            var response = await _httpClient.PostAsync($"api/workspaces/{workspaceId}/documents", formData, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var doc = await response.Content.ReadFromJsonAsync<DocumentDto>(_jsonOptions, cancellationToken);
                return Result.Success(doc!);
            }

            return await ExtractErrorAsync<DocumentDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<DocumentDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/documents/{documentId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<byte[]>> DownloadDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        SetAuthorizationHeader();
        try
        {
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/documents/{documentId}/download", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return Result.Success(bytes);
            }

            return await ExtractErrorAsync<byte[]>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<DocumentChunkDto>>> ChunkDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsync($"/api/workspaces/{workspaceId}/documents/{documentId}/chunk", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<DocumentChunkDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<DocumentChunkDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<GenerateEmbeddingsResponse>> EmbedDocumentAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsync($"/api/workspaces/{workspaceId}/documents/{documentId}/embed", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<GenerateEmbeddingsResponse>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<GenerateEmbeddingsResponse>(Error.NullValue);
            }

            return await ExtractErrorAsync<GenerateEmbeddingsResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<GenerateEmbeddingsResponse>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<DocumentChunkDto>>> GetDocumentChunksAsync(Guid workspaceId, Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"/api/workspaces/{workspaceId}/documents/{documentId}/chunks", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<DocumentChunkDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<DocumentChunkDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<IReadOnlyList<DocumentChunkDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<DocumentChunkDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<PagedResult<SearchResultDto>>> SearchAsync(Guid workspaceId, SearchRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var query = Uri.EscapeDataString(request.Query ?? string.Empty);
            var url = $"/api/workspaces/{workspaceId}/search?q={query}&page={request.Page}&pageSize={request.PageSize}";
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                url += $"&type={Uri.EscapeDataString(request.Type)}";
            }
            if (!string.IsNullOrWhiteSpace(request.Mode))
            {
                url += $"&mode={Uri.EscapeDataString(request.Mode)}";
            }
            if (request.TopK.HasValue)
            {
                url += $"&topK={request.TopK.Value}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PagedResult<SearchResultDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<PagedResult<SearchResultDto>>(Error.NullValue);
            }

            return await ExtractErrorAsync<PagedResult<SearchResultDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PagedResult<SearchResultDto>>(new Error("Search.NetworkError", ex.Message));
        }
    }

    #endregion

    private async Task<Result<T>> ExtractErrorAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var errorDoc = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions, cancellationToken);
            if (errorDoc.TryGetProperty("description", out var desc) || errorDoc.TryGetProperty("Description", out desc))
            {
                var code = errorDoc.TryGetProperty("code", out var c) || errorDoc.TryGetProperty("Code", out c) ? c.GetString() : "API.Error";
                return Result.Failure<T>(new Error(code ?? "API.Error", desc.GetString() ?? "Request failed."));
            }
        }
        catch
        {
            // Ignore JSON parse errors on non-standard error bodies
        }

        return Result.Failure<T>(new Error("API.Error", $"Request failed with status code {response.StatusCode}"));
    }

    // AI Conversations & Chat
    public async Task<Result<IReadOnlyList<ConversationDto>>> GetConversationsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/conversations", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<ConversationDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<ConversationDto>>(Error.NullValue);
            }
            return await ExtractErrorAsync<IReadOnlyList<ConversationDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<ConversationDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<ConversationDto>> CreateConversationAsync(Guid workspaceId, CreateConversationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/conversations", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ConversationDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<ConversationDto>(Error.NullValue);
            }
            return await ExtractErrorAsync<ConversationDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<ConversationDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<ConversationDto>> GetConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/conversations/{conversationId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ConversationDto>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<ConversationDto>(Error.NullValue);
            }
            return await ExtractErrorAsync<ConversationDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<ConversationDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<ChatMessageDto>>> GetConversationMessagesAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/conversations/{conversationId}/messages", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IReadOnlyList<ChatMessageDto>>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<IReadOnlyList<ChatMessageDto>>(Error.NullValue);
            }
            return await ExtractErrorAsync<IReadOnlyList<ChatMessageDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<ChatMessageDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<SendChatMessageResponse>> SendChatMessageAsync(Guid workspaceId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/conversations/{conversationId}/messages", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<SendChatMessageResponse>(_jsonOptions, cancellationToken);
                return data != null ? Result.Success(data) : Result.Failure<SendChatMessageResponse>(Error.NullValue);
            }
            return await ExtractErrorAsync<SendChatMessageResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<SendChatMessageResponse>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<bool>> ArchiveConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsync($"api/workspaces/{workspaceId}/conversations/{conversationId}/archive", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }
            return await ExtractErrorAsync<bool>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<bool>> DeleteConversationAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/conversations/{conversationId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }
            return await ExtractErrorAsync<bool>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<AiOperationResultDto>> SummarizeAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAiOperationAsync($"api/workspaces/{workspaceId}/ai/summarize", request, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> ExplainAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAiOperationAsync($"api/workspaces/{workspaceId}/ai/explain", request, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> ExtractKeyPointsAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAiOperationAsync($"api/workspaces/{workspaceId}/ai/key-points", request, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> GenerateQuestionsAsync(Guid workspaceId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAiOperationAsync($"api/workspaces/{workspaceId}/ai/questions", request, cancellationToken);
    }

    public async Task<Result<AiOperationResultDto>> GenerateStudyMaterialAsync(Guid workspaceId, GenerateStudyMaterialRequest request, CancellationToken cancellationToken = default)
    {
        return await PostAiOperationAsync($"api/workspaces/{workspaceId}/ai/study-material", request, cancellationToken);
    }

    public async Task<Result<NoteDto>> SaveAiOutputAsNoteAsync(Guid workspaceId, SaveAiOutputAsNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/ai/save-as-note", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var note = await response.Content.ReadFromJsonAsync<NoteDto>(_jsonOptions, cancellationToken);
                return Result.Success(note!);
            }
            return await ExtractErrorAsync<NoteDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NoteDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<AiGenerationSummaryDto>>> GetAiGenerationsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/ai/generations", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<AiGenerationSummaryDto>>(_jsonOptions, cancellationToken);
                return Result.Success<IReadOnlyList<AiGenerationSummaryDto>>(list ?? new List<AiGenerationSummaryDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<AiGenerationSummaryDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<AiGenerationSummaryDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<AiOperationResultDto>> GetAiGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/ai/generations/{generationId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<AiOperationResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<AiOperationResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<AiOperationResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<bool>> DeleteAiGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/ai/generations/{generationId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }
            return await ExtractErrorAsync<bool>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(new Error("Network.Error", ex.Message));
        }
    }

    private async Task<Result<AiOperationResultDto>> PostAiOperationAsync<TReq>(string url, TReq request, CancellationToken cancellationToken)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AiOperationResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(result!);
            }
            return await ExtractErrorAsync<AiOperationResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<AiOperationResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    #region Study Engine & AI Tutor (Phase 6)

    public async Task<Result<IReadOnlyList<StudyTopicDto>>> GetTopicsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/study/topics", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<StudyTopicDto>>(_jsonOptions, cancellationToken);
                return Result.Success(list ?? Array.Empty<StudyTopicDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<StudyTopicDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<StudyTopicDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<StudyTopicDto>> CreateTopicAsync(Guid workspaceId, CreateStudyTopicRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/topics", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<StudyTopicDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<StudyTopicDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<StudyTopicDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<StudyTopicDto>> UpdateTopicAsync(Guid workspaceId, Guid topicId, UpdateStudyTopicRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/study/topics/{topicId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<StudyTopicDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<StudyTopicDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<StudyTopicDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<bool>> DeleteTopicAsync(Guid workspaceId, Guid topicId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/study/topics/{topicId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }
            return await ExtractErrorAsync<bool>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<StudySessionDto>>> GetStudySessionsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/sessions" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<StudySessionDto>>(_jsonOptions, cancellationToken);
                return Result.Success(list ?? Array.Empty<StudySessionDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<StudySessionDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<StudySessionDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<StudySessionDto>> StartStudySessionAsync(Guid workspaceId, Guid? topicId, StartStudySessionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/sessions" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<StudySessionDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<StudySessionDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<StudySessionDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<StudySessionDto>> CompleteStudySessionAsync(Guid workspaceId, Guid sessionId, CompleteStudySessionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/sessions/{sessionId}/complete", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<StudySessionDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<StudySessionDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<StudySessionDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<FlashcardDto>>> GetFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/flashcards" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<FlashcardDto>>(_jsonOptions, cancellationToken);
                return Result.Success(list ?? Array.Empty<FlashcardDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<FlashcardDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<FlashcardDto>>> GetDueFlashcardsAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/flashcards/due" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<FlashcardDto>>(_jsonOptions, cancellationToken);
                return Result.Success(list ?? Array.Empty<FlashcardDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<FlashcardDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<FlashcardDto>> CreateFlashcardAsync(Guid workspaceId, Guid? topicId, CreateFlashcardRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/flashcards" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<FlashcardDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<FlashcardDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<FlashcardDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<FlashcardDto>> ReviewFlashcardAsync(Guid workspaceId, Guid flashcardId, ReviewFlashcardRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/flashcards/{flashcardId}/review", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<FlashcardDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<FlashcardDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<FlashcardDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<FlashcardDto>>> GenerateFlashcardsAsync(Guid workspaceId, Guid? topicId, GenerateFlashcardsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/flashcards/generate" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<FlashcardDto>>(_jsonOptions, cancellationToken);
                return Result.Success(list ?? Array.Empty<FlashcardDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<FlashcardDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<FlashcardDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<bool>> DeleteFlashcardAsync(Guid workspaceId, Guid flashcardId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/study/flashcards/{flashcardId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }
            return await ExtractErrorAsync<bool>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<QuizDto>>> GetQuizzesAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/quizzes" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<QuizDto>>(_jsonOptions, cancellationToken);
                return Result.Success(list ?? Array.Empty<QuizDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<QuizDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<QuizDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<SafeQuizDetailDto>> GetSafeQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/study/quizzes/{quizId}/safe", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<SafeQuizDetailDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<SafeQuizDetailDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<SafeQuizDetailDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<QuizDetailDto>> GetQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/study/quizzes/{quizId}?safe=false", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<QuizDetailDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<QuizDetailDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<QuizDetailDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<QuizDetailDto>> GenerateQuizAsync(Guid workspaceId, Guid? topicId, GenerateQuizRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/quizzes/generate" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<QuizDetailDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<QuizDetailDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<QuizDetailDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<QuizAttemptResultDto>> StartQuizAttemptAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsync($"api/workspaces/{workspaceId}/study/quizzes/{quizId}/attempts", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<QuizAttemptResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<QuizAttemptResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<QuizAttemptResultDto>> SubmitQuizAttemptAsync(Guid workspaceId, Guid attemptId, SubmitQuizAttemptRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/attempts/{attemptId}/submit", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<QuizAttemptResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<QuizAttemptResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<QuizAttemptResultDto>> GetQuizAttemptResultAsync(Guid workspaceId, Guid attemptId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/study/attempts/{attemptId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<QuizAttemptResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<QuizAttemptResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<QuizAttemptResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<bool>> DeleteQuizAsync(Guid workspaceId, Guid quizId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/study/quizzes/{quizId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success(true);
            }
            return await ExtractErrorAsync<bool>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<KnowledgeAssessmentDto>> GetAssessmentAsync(Guid workspaceId, Guid? topicId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var url = $"api/workspaces/{workspaceId}/study/assessment" + (topicId.HasValue ? $"?topicId={topicId.Value}" : "");
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<KnowledgeAssessmentDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<KnowledgeAssessmentDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<KnowledgeAssessmentDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<StudyDashboardDto>> GetStudyDashboardAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/study/dashboard", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<StudyDashboardDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<StudyDashboardDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<StudyDashboardDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<TutorResponseDto>> TutorChatAsync(Guid workspaceId, TutorChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/tutor/chat", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<TutorResponseDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<TutorResponseDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<TutorResponseDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<TutorHintDto>> TutorHintAsync(Guid workspaceId, TutorHintRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/tutor/hint", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<TutorHintDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<TutorHintDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<TutorHintDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<TutorExplanationDto>> TutorExplainWrongAsync(Guid workspaceId, TutorExplainWrongAnswerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/tutor/explain-wrong", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<TutorExplanationDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<TutorExplanationDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<TutorExplanationDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<TutorMiniExerciseDto>> TutorExerciseAsync(Guid workspaceId, TutorMiniExerciseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/study/tutor/exercise", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<TutorMiniExerciseDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<TutorMiniExerciseDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<TutorMiniExerciseDto>(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    #region Visual Thinking Methods

    // Boards
    public async Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/boards", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<BoardDto>>(_jsonOptions, cancellationToken);
                return Result.Success(items ?? Array.Empty<BoardDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<BoardDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<BoardDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<BoardDetailDto>> GetBoardByIdAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/boards/{boardId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<BoardDetailDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<BoardDetailDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<BoardDetailDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<BoardDto>> CreateBoardAsync(Guid workspaceId, CreateBoardRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/boards", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<BoardDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<BoardDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<BoardDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<BoardDto>> UpdateBoardAsync(Guid workspaceId, Guid boardId, UpdateBoardRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/boards/{boardId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<BoardDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<BoardDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<BoardDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteBoardAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/boards/{boardId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }
            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BoardItemDto>>> GetBoardItemsAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/boards/{boardId}/items", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<BoardItemDto>>(_jsonOptions, cancellationToken);
                return Result.Success(items ?? Array.Empty<BoardItemDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<BoardItemDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<BoardItemDto>> CreateBoardItemAsync(Guid workspaceId, Guid boardId, CreateBoardItemRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/boards/{boardId}/items", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<BoardItemDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<BoardItemDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<BoardItemDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<BoardItemDto>> UpdateBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, UpdateBoardItemRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/boards/{boardId}/items/{itemId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<BoardItemDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<BoardItemDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<BoardItemDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/boards/{boardId}/items/{itemId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }
            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BoardItemDto>>> BatchUpdateBoardItemsAsync(Guid workspaceId, Guid boardId, BatchUpdateBoardItemsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PatchAsJsonAsync($"api/workspaces/{workspaceId}/boards/{boardId}/items/batch", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<BoardItemDto>>(_jsonOptions, cancellationToken);
                return Result.Success(items ?? Array.Empty<BoardItemDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<BoardItemDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Network.Error", ex.Message));
        }
    }

    // Mind Maps
    public async Task<Result<IReadOnlyList<MindMapDto>>> GetMindMapsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/mindmaps", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<MindMapDto>>(_jsonOptions, cancellationToken);
                return Result.Success(items ?? Array.Empty<MindMapDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<MindMapDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<MindMapDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapDetailDto>> GetMindMapByIdAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapDetailDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapDetailDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapDetailDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapDto>> CreateMindMapAsync(Guid workspaceId, CreateMindMapRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapDto>> UpdateMindMapAsync(Guid workspaceId, Guid mindMapId, UpdateMindMapRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteMindMapAsync(Guid workspaceId, Guid mindMapId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }
            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapNodeDto>> CreateMindMapNodeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapNodeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapNodeDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapNodeDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapNodeDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapNodeDto>> UpdateMindMapNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, UpdateMindMapNodeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes/{nodeId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapNodeDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapNodeDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapNodeDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteMindMapNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes/{nodeId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }
            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<MindMapNodeDto>>> BatchUpdateMindMapNodesAsync(Guid workspaceId, Guid mindMapId, BatchUpdateMindMapNodesRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PatchAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes/batch", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<MindMapNodeDto>>(_jsonOptions, cancellationToken);
                return Result.Success(items ?? Array.Empty<MindMapNodeDto>());
            }
            return await ExtractErrorAsync<IReadOnlyList<MindMapNodeDto>>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<MindMapNodeDto>>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapEdgeDto>> CreateMindMapEdgeAsync(Guid workspaceId, Guid mindMapId, CreateMindMapEdgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/edges", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapEdgeDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapEdgeDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<MindMapEdgeDto>> UpdateMindMapEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, UpdateMindMapEdgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/edges/{edgeId}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<MindMapEdgeDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<MindMapEdgeDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<MindMapEdgeDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteMindMapEdgeAsync(Guid workspaceId, Guid mindMapId, Guid edgeId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/edges/{edgeId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }
            return await ExtractErrorAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<LayoutResultDto>> ApplyMindMapLayoutAsync(Guid workspaceId, Guid mindMapId, ApplyLayoutRequest request, bool persist = false, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/layout?persist={persist}", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<LayoutResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<LayoutResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<LayoutResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NodeKnowledgeContextDto>> GetNodeKnowledgeContextAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes/{nodeId}/knowledge", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<NodeKnowledgeContextDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<NodeKnowledgeContextDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NodeKnowledgeContextDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<GeneratedMindMapDto>> GenerateMindMapAsync(Guid workspaceId, GenerateMindMapRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/generate", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<GeneratedMindMapDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<GeneratedMindMapDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<GeneratedMindMapDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<NodeExplanationDto>> ExplainNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, ExplainNodeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes/{nodeId}/explain", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<NodeExplanationDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<NodeExplanationDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<NodeExplanationDto>(new Error("Network.Error", ex.Message));
        }
    }

    public async Task<Result<RelatedKnowledgeResultDto>> FindRelatedKnowledgeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, FindRelatedKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync($"api/workspaces/{workspaceId}/mindmaps/{mindMapId}/nodes/{nodeId}/related-knowledge", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<RelatedKnowledgeResultDto>(_jsonOptions, cancellationToken);
                return Result.Success(item!);
            }
            return await ExtractErrorAsync<RelatedKnowledgeResultDto>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<RelatedKnowledgeResultDto>(new Error("Network.Error", ex.Message));
        }
    }

    #endregion

    private async Task<Result> ExtractErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var res = await ExtractErrorAsync<object>(response, cancellationToken);
        return Result.Failure(res.Error);
    }
}
