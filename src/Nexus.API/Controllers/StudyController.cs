using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexus.Application.DTOs.Study;
using Nexus.Application.Features.Study.Services;

namespace Nexus.API.Controllers;

[Authorize]
[Route("api/workspaces/{workspaceId:guid}/study")]
public class StudyController : ApiControllerBase
{
    private readonly IStudyTopicService _topicService;
    private readonly IStudySessionService _sessionService;
    private readonly IFlashcardService _flashcardService;
    private readonly IQuizService _quizService;
    private readonly IKnowledgeAssessmentService _assessmentService;
    private readonly IAiTutorService _tutorService;

    public StudyController(
        IStudyTopicService topicService,
        IStudySessionService sessionService,
        IFlashcardService flashcardService,
        IQuizService quizService,
        IKnowledgeAssessmentService assessmentService,
        IAiTutorService tutorService)
    {
        _topicService = topicService;
        _sessionService = sessionService;
        _flashcardService = flashcardService;
        _quizService = quizService;
        _assessmentService = assessmentService;
        _tutorService = tutorService;
    }

    // ==================== Topics ====================

    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics(Guid workspaceId, CancellationToken ct)
    {
        var result = await _topicService.GetTopicsAsync(workspaceId, ct);
        return HandleResult(result);
    }

    [HttpGet("topics/{topicId:guid}")]
    public async Task<IActionResult> GetTopicById(Guid workspaceId, Guid topicId, CancellationToken ct)
    {
        var result = await _topicService.GetTopicByIdAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic(
        Guid workspaceId,
        [FromBody] CreateStudyTopicRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _topicService.CreateTopicAsync(workspaceId, request, ct);
        return HandleResult(result);
    }

    [HttpPut("topics/{topicId:guid}")]
    public async Task<IActionResult> UpdateTopic(
        Guid workspaceId,
        Guid topicId,
        [FromBody] UpdateStudyTopicRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _topicService.UpdateTopicAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("topics/{topicId:guid}")]
    public async Task<IActionResult> DeleteTopic(Guid workspaceId, Guid topicId, CancellationToken ct)
    {
        var result = await _topicService.DeleteTopicAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    // ==================== Sessions ====================

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        CancellationToken ct)
    {
        var result = await _sessionService.GetSessionsAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionById(Guid workspaceId, Guid sessionId, CancellationToken ct)
    {
        var result = await _sessionService.GetSessionByIdAsync(workspaceId, sessionId, ct);
        return HandleResult(result);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        [FromBody] StartStudySessionRequest? request,
        CancellationToken ct)
    {
        var result = await _sessionService.StartSessionAsync(workspaceId, topicId, request ?? new StartStudySessionRequest(), ct);
        return HandleResult(result);
    }

    [HttpPost("topics/{topicId:guid}/sessions")]
    public async Task<IActionResult> StartTopicSession(
        Guid workspaceId,
        Guid topicId,
        [FromBody] StartStudySessionRequest? request,
        CancellationToken ct)
    {
        var result = await _sessionService.StartSessionAsync(workspaceId, topicId, request ?? new StartStudySessionRequest(), ct);
        return HandleResult(result);
    }

    [HttpPost("sessions/{sessionId:guid}/complete")]
    public async Task<IActionResult> CompleteSession(
        Guid workspaceId,
        Guid sessionId,
        [FromBody] CompleteStudySessionRequest? request,
        CancellationToken ct)
    {
        var result = await _sessionService.CompleteSessionAsync(workspaceId, sessionId, request ?? new CompleteStudySessionRequest(), ct);
        return HandleResult(result);
    }

    // ==================== Flashcards ====================

    [HttpGet("flashcards")]
    public async Task<IActionResult> GetFlashcards(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        CancellationToken ct)
    {
        var result = await _flashcardService.GetFlashcardsAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("topics/{topicId:guid}/flashcards")]
    public async Task<IActionResult> GetTopicFlashcards(Guid workspaceId, Guid topicId, CancellationToken ct)
    {
        var result = await _flashcardService.GetFlashcardsAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("flashcards/due")]
    public async Task<IActionResult> GetDueFlashcards(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        CancellationToken ct)
    {
        var result = await _flashcardService.GetDueFlashcardsAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("topics/{topicId:guid}/flashcards/due")]
    public async Task<IActionResult> GetTopicDueFlashcards(Guid workspaceId, Guid topicId, CancellationToken ct)
    {
        var result = await _flashcardService.GetDueFlashcardsAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("flashcards/{flashcardId:guid}")]
    public async Task<IActionResult> GetFlashcardById(Guid workspaceId, Guid flashcardId, CancellationToken ct)
    {
        var result = await _flashcardService.GetFlashcardByIdAsync(workspaceId, flashcardId, ct);
        return HandleResult(result);
    }

    [HttpPost("flashcards")]
    public async Task<IActionResult> CreateFlashcard(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        [FromBody] CreateFlashcardRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _flashcardService.CreateFlashcardAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("topics/{topicId:guid}/flashcards")]
    public async Task<IActionResult> CreateTopicFlashcard(
        Guid workspaceId,
        Guid topicId,
        [FromBody] CreateFlashcardRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _flashcardService.CreateFlashcardAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("flashcards/{flashcardId:guid}/review")]
    public async Task<IActionResult> ReviewFlashcard(
        Guid workspaceId,
        Guid flashcardId,
        [FromBody] ReviewFlashcardRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _flashcardService.ReviewFlashcardAsync(workspaceId, flashcardId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("flashcards/generate")]
    public async Task<IActionResult> GenerateFlashcards(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        [FromBody] GenerateFlashcardsRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _flashcardService.GenerateFlashcardsAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("topics/{topicId:guid}/flashcards/generate")]
    public async Task<IActionResult> GenerateTopicFlashcards(
        Guid workspaceId,
        Guid topicId,
        [FromBody] GenerateFlashcardsRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _flashcardService.GenerateFlashcardsAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("flashcards/{flashcardId:guid}")]
    public async Task<IActionResult> DeleteFlashcard(Guid workspaceId, Guid flashcardId, CancellationToken ct)
    {
        var result = await _flashcardService.DeleteFlashcardAsync(workspaceId, flashcardId, ct);
        return HandleResult(result);
    }

    // ==================== Quizzes ====================

    [HttpGet("quizzes")]
    public async Task<IActionResult> GetQuizzes(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        CancellationToken ct)
    {
        var result = await _quizService.GetQuizzesAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("topics/{topicId:guid}/quizzes")]
    public async Task<IActionResult> GetTopicQuizzes(Guid workspaceId, Guid topicId, CancellationToken ct)
    {
        var result = await _quizService.GetQuizzesAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("quizzes/{quizId:guid}")]
    public async Task<IActionResult> GetQuizById(
        Guid workspaceId,
        Guid quizId,
        [FromQuery] bool safe = true,
        CancellationToken ct = default)
    {
        if (safe)
        {
            var safeResult = await _quizService.GetSafeQuizByIdAsync(workspaceId, quizId, ct);
            return HandleResult(safeResult);
        }

        var fullResult = await _quizService.GetQuizByIdAsync(workspaceId, quizId, ct);
        return HandleResult(fullResult);
    }

    [HttpGet("quizzes/{quizId:guid}/safe")]
    public async Task<IActionResult> GetSafeQuizById(Guid workspaceId, Guid quizId, CancellationToken ct)
    {
        var result = await _quizService.GetSafeQuizByIdAsync(workspaceId, quizId, ct);
        return HandleResult(result);
    }

    [HttpPost("quizzes/generate")]
    public async Task<IActionResult> GenerateQuiz(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        [FromBody] GenerateQuizRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _quizService.GenerateQuizAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("topics/{topicId:guid}/quizzes/generate")]
    public async Task<IActionResult> GenerateTopicQuiz(
        Guid workspaceId,
        Guid topicId,
        [FromBody] GenerateQuizRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _quizService.GenerateQuizAsync(workspaceId, topicId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("quizzes/{quizId:guid}/attempts")]
    public async Task<IActionResult> StartQuizAttempt(Guid workspaceId, Guid quizId, CancellationToken ct)
    {
        var result = await _quizService.StartQuizAttemptAsync(workspaceId, quizId, ct);
        return HandleResult(result);
    }

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<IActionResult> SubmitQuizAttempt(
        Guid workspaceId,
        Guid attemptId,
        [FromBody] SubmitQuizAttemptRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _quizService.SubmitQuizAttemptAsync(workspaceId, attemptId, request, ct);
        return HandleResult(result);
    }

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<IActionResult> GetAttemptResult(Guid workspaceId, Guid attemptId, CancellationToken ct)
    {
        var result = await _quizService.GetAttemptResultAsync(workspaceId, attemptId, ct);
        return HandleResult(result);
    }

    [HttpDelete("quizzes/{quizId:guid}")]
    public async Task<IActionResult> DeleteQuiz(Guid workspaceId, Guid quizId, CancellationToken ct)
    {
        var result = await _quizService.DeleteQuizAsync(workspaceId, quizId, ct);
        return HandleResult(result);
    }

    // ==================== Assessment & Dashboard ====================

    [HttpGet("assessment")]
    public async Task<IActionResult> GetAssessment(
        Guid workspaceId,
        [FromQuery] Guid? topicId,
        CancellationToken ct)
    {
        var result = await _assessmentService.GetAssessmentAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("topics/{topicId:guid}/assessment")]
    public async Task<IActionResult> GetTopicAssessment(Guid workspaceId, Guid topicId, CancellationToken ct)
    {
        var result = await _assessmentService.GetAssessmentAsync(workspaceId, topicId, ct);
        return HandleResult(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(Guid workspaceId, CancellationToken ct)
    {
        var result = await _assessmentService.GetDashboardAsync(workspaceId, ct);
        return HandleResult(result);
    }

    // ==================== AI Tutor ====================

    [HttpPost("tutor/chat")]
    public async Task<IActionResult> TutorChat(
        Guid workspaceId,
        [FromBody] TutorChatRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _tutorService.ChatWithTutorAsync(workspaceId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("topics/{topicId:guid}/tutor/chat")]
    public async Task<IActionResult> TutorTopicChat(
        Guid workspaceId,
        Guid topicId,
        [FromBody] TutorChatRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var updatedRequest = request with { StudyTopicId = topicId };
        var result = await _tutorService.ChatWithTutorAsync(workspaceId, updatedRequest, ct);
        return HandleResult(result);
    }

    [HttpPost("tutor/hint")]
    public async Task<IActionResult> TutorHint(
        Guid workspaceId,
        [FromBody] TutorHintRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _tutorService.GetHintAsync(workspaceId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("tutor/explain-wrong")]
    public async Task<IActionResult> TutorExplainWrong(
        Guid workspaceId,
        [FromBody] TutorExplainWrongAnswerRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _tutorService.ExplainWrongAnswerAsync(workspaceId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("tutor/exercise")]
    public async Task<IActionResult> TutorExercise(
        Guid workspaceId,
        [FromBody] TutorMiniExerciseRequest request,
        CancellationToken ct)
    {
        if (request == null) return BadRequest("Request body is required.");
        var result = await _tutorService.GenerateMiniExerciseAsync(workspaceId, request, ct);
        return HandleResult(result);
    }
}
