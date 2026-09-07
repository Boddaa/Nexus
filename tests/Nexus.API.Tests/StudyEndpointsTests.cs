using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Study;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.API.Tests;

public class StudyEndpointsTests : IClassFixture<AiMockWebApplicationFactory>
{
    private readonly AiMockWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StudyEndpointsTests(AiMockWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient authenticatedClient, AuthResponse user, WorkspaceSummaryDto defaultWorkspace)> CreateUserAndWorkspaceAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@nexus.ai";
        var password = "SecurePassword123!";
        var registerRequest = new RegisterRequest(email, password, $"{prefix} User");

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        regResponse.EnsureSuccessStatusCode();

        var authData = (await regResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var requestClient = _factory.CreateClient();
        requestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);

        var wsResponse = await requestClient.GetAsync("/api/workspaces");
        wsResponse.EnsureSuccessStatusCode();
        var workspaces = (await wsResponse.Content.ReadFromJsonAsync<List<WorkspaceSummaryDto>>())!;

        return (requestClient, authData, workspaces.First());
    }

    [Fact]
    public async Task StudyEndpoints_Without_Authentication_Should_Return_401()
    {
        var wsId = Guid.NewGuid();
        var res1 = await _client.GetAsync($"/api/workspaces/{wsId}/study/topics");
        Assert.Equal(HttpStatusCode.Unauthorized, res1.StatusCode);

        var res2 = await _client.GetAsync($"/api/workspaces/{wsId}/study/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, res2.StatusCode);

        var res3 = await _client.GetAsync($"/api/workspaces/{wsId}/study/flashcards");
        Assert.Equal(HttpStatusCode.Unauthorized, res3.StatusCode);
    }

    [Fact]
    public async Task Topics_CRUD_And_WorkspaceIsolation_Should_Be_Enforced()
    {
        var (clientAlice, _, workspaceAlice) = await CreateUserAndWorkspaceAsync("study_alice");
        var (clientBob, _, workspaceBob) = await CreateUserAndWorkspaceAsync("study_bob");

        // 1. Alice creates topic
        var createReq = new CreateStudyTopicRequest("Machine Learning", "Deep learning & Transformers");
        var createRes = await clientAlice.PostAsJsonAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics", createReq);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);

        var createdTopic = await createRes.Content.ReadFromJsonAsync<StudyTopicDto>();
        Assert.NotNull(createdTopic);
        Assert.Equal("Machine Learning", createdTopic.Title);

        // 2. Alice lists topics
        var listRes = await clientAlice.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var topics = await listRes.Content.ReadFromJsonAsync<List<StudyTopicDto>>();
        Assert.NotNull(topics);
        Assert.Contains(topics, t => t.Id == createdTopic.Id);

        // 3. Bob tries to access Alice's workspace topics -> Denied
        var bobAccess = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics");
        Assert.True(bobAccess.StatusCode == HttpStatusCode.Unauthorized || bobAccess.StatusCode == HttpStatusCode.Forbidden);

        // 4. Alice updates topic
        var updateReq = new UpdateStudyTopicRequest("Advanced Machine Learning", "Diffusion models");
        var updateRes = await clientAlice.PutAsJsonAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics/{createdTopic.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 5. Alice deletes topic
        var deleteRes = await clientAlice.DeleteAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics/{createdTopic.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);
    }

    [Fact]
    public async Task StudySessions_Lifecycle_Should_StartAndCompleteSuccessfully()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("study_session");

        var startReq = new StartStudySessionRequest("Core CS Study", "Starting data structures");
        var startRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/study/sessions", startReq);
        Assert.Equal(HttpStatusCode.OK, startRes.StatusCode);

        var session = await startRes.Content.ReadFromJsonAsync<StudySessionDto>();
        Assert.NotNull(session);
        Assert.Equal(StudySessionStatus.InProgress, session.Status);

        var completeReq = new CompleteStudySessionRequest(DurationMinutes: 30, ItemsAttempted: 15, ItemsCompleted: 15, Notes: "Great focus session");
        var completeRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/study/sessions/{session.Id}/complete", completeReq);
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);

        var completedSession = await completeRes.Content.ReadFromJsonAsync<StudySessionDto>();
        Assert.NotNull(completedSession);
        Assert.Equal(StudySessionStatus.Completed, completedSession.Status);
        Assert.Equal(30, completedSession.DurationMinutes);
    }

    [Fact]
    public async Task Flashcards_CreateAndReview_Should_UpdateSpacedRepetitionStats()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("study_cards");

        var cardReq = new CreateFlashcardRequest("What is idempotency?", "An operation that can be applied multiple times without changing the result.", "Medium");
        var createRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/study/flashcards", cardReq);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);

        var card = await createRes.Content.ReadFromJsonAsync<FlashcardDto>();
        Assert.NotNull(card);
        Assert.Equal(0, card.ReviewCount);

        // Review with Good rating
        var reviewReq = new ReviewFlashcardRequest(ReviewRating.Good);
        var reviewRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/study/flashcards/{card.Id}/review", reviewReq);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

        var reviewedCard = await reviewRes.Content.ReadFromJsonAsync<FlashcardDto>();
        Assert.NotNull(reviewedCard);
        Assert.Equal(1, reviewedCard.ReviewCount);
        Assert.Equal(1, reviewedCard.CorrectCount);
        Assert.Equal(0, reviewedCard.WrongCount);
        Assert.True(reviewedCard.NextReviewAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task StudyDashboard_And_Assessment_Should_ReturnSuccess()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("study_dash");

        var dashRes = await client.GetAsync($"/api/workspaces/{workspace.Id}/study/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashRes.StatusCode);

        var dash = await dashRes.Content.ReadFromJsonAsync<StudyDashboardDto>();
        Assert.NotNull(dash);
        Assert.True(dash.TotalTopics >= 0);

        var assessRes = await client.GetAsync($"/api/workspaces/{workspace.Id}/study/assessment");
        Assert.Equal(HttpStatusCode.OK, assessRes.StatusCode);

        var assessment = await assessRes.Content.ReadFromJsonAsync<KnowledgeAssessmentDto>();
        Assert.NotNull(assessment);
        Assert.NotNull(assessment.RecommendedActions);
    }

    [Fact]
    public async Task AiTutor_Chat_Should_Return_TutorResponse()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("study_tutor");

        var tutorReq = new TutorChatRequest("Hello Tutor! Can you explain the basics of concurrency?");
        var res = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/study/tutor/chat", tutorReq);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var tutorResponse = await res.Content.ReadFromJsonAsync<TutorResponseDto>();
        Assert.NotNull(tutorResponse);
        Assert.False(string.IsNullOrWhiteSpace(tutorResponse.AssistantMessage));
        Assert.NotNull(tutorResponse.KeyTakeaways);
        Assert.NotNull(tutorResponse.FollowUpSuggestions);
    }

    [Fact]
    public async Task QuizAttempt_UserIsolation_UserACannotSubmitUserBAttempt_ReturnsNotFound()
    {
        var (clientAlice, userAlice, workspaceAlice) = await CreateUserAndWorkspaceAsync("quiz_iso_a");
        var (clientBob, userBob, _) = await CreateUserAndWorkspaceAsync("quiz_iso_b");

        // Seed Quiz and add Bob as WorkspaceMember
        Guid quizId;
        Guid qnId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quiz = new Quiz { WorkspaceId = workspaceAlice.Id, UserId = userAlice.UserId, Title = "API Isolation Quiz" };
            var qn = new QuizQuestion { Quiz = quiz, QuestionText = "Is isolation secure?", CorrectAnswer = "Yes", OptionsJson = "[\"Yes\"]" };
            db.Quizzes.Add(quiz);
            db.QuizQuestions.Add(qn);
            db.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceAlice.Id, UserId = userBob.UserId, Role = WorkspaceRole.Editor });
            db.SaveChanges();

            quizId = quiz.Id;
            qnId = qn.Id;
        }

        // Alice starts the quiz attempt
        var startRes = await clientAlice.PostAsync($"/api/workspaces/{workspaceAlice.Id}/study/quizzes/{quizId}/attempts", null);
        Assert.Equal(HttpStatusCode.OK, startRes.StatusCode);

        var startData = await startRes.Content.ReadFromJsonAsync<QuizAttemptResultDto>();
        Assert.NotNull(startData);
        var attemptId = startData.AttemptId;

        // Bob tries to submit Alice's attempt -> Should return 404 (QuizAttempt.NotFound due to user mismatch)
        var submitReq = new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(qnId, "Yes")
        });
        var bobSubmitRes = await clientBob.PostAsJsonAsync($"/api/workspaces/{workspaceAlice.Id}/study/attempts/{attemptId}/submit", submitReq);
        Assert.Equal(HttpStatusCode.NotFound, bobSubmitRes.StatusCode);

        // Bob tries to view Alice's attempt result -> Should return 404
        var bobGetRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/attempts/{attemptId}");
        Assert.Equal(HttpStatusCode.NotFound, bobGetRes.StatusCode);
    }

    [Fact]
    public async Task QuizSubmission_RejectsInvalidQuestionId_ReturnsBadRequest()
    {
        var (client, user, workspace) = await CreateUserAndWorkspaceAsync("quiz_invalid_q");

        Guid quizId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quiz = new Quiz { WorkspaceId = workspace.Id, UserId = user.UserId, Title = "Validation Test Quiz" };
            var qn = new QuizQuestion { Quiz = quiz, QuestionText = "Q?", CorrectAnswer = "A", OptionsJson = "[\"A\"]" };
            db.Quizzes.Add(quiz);
            db.QuizQuestions.Add(qn);
            db.SaveChanges();
            quizId = quiz.Id;
        }

        var startRes = await client.PostAsync($"/api/workspaces/{workspace.Id}/study/quizzes/{quizId}/attempts", null);
        Assert.Equal(HttpStatusCode.OK, startRes.StatusCode);
        var startData = await startRes.Content.ReadFromJsonAsync<QuizAttemptResultDto>();
        Assert.NotNull(startData);

        // Submit with non-existent question ID
        var fakeQnId = Guid.NewGuid();
        var submitReq = new SubmitQuizAttemptRequest(new List<SubmitQuizAnswerDto>
        {
            new(fakeQnId, "A")
        });

        var submitRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/study/attempts/{startData.AttemptId}/submit", submitReq);
        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
    }

    [Fact]
    public async Task Quiz_SafeVsFullDetail_Authorization_Enforced()
    {
        var (clientAlice, userAlice, workspaceAlice) = await CreateUserAndWorkspaceAsync("quiz_auth_a");
        var (clientBob, userBob, _) = await CreateUserAndWorkspaceAsync("quiz_auth_b");

        Guid quizId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quiz = new Quiz { WorkspaceId = workspaceAlice.Id, UserId = userAlice.UserId, Title = "Answer Key Protection Quiz" };
            var qn = new QuizQuestion { Quiz = quiz, QuestionText = "Secret Q?", CorrectAnswer = "TopSecretAnswer", OptionsJson = "[\"TopSecretAnswer\"]" };
            db.Quizzes.Add(quiz);
            db.QuizQuestions.Add(qn);
            db.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceAlice.Id, UserId = userBob.UserId, Role = WorkspaceRole.Editor });
            db.SaveChanges();
            quizId = quiz.Id;
        }

        // 1. Bob requests safe quiz -> 200 OK, questions are safe
        var safeRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/quizzes/{quizId}?safe=true");
        Assert.Equal(HttpStatusCode.OK, safeRes.StatusCode);
        var safeQuiz = await safeRes.Content.ReadFromJsonAsync<SafeQuizDetailDto>();
        Assert.NotNull(safeQuiz);
        Assert.Single(safeQuiz.Questions);
        Assert.Equal("Secret Q?", safeQuiz.Questions[0].QuestionText);

        // 2. Bob requests full quiz (safe=false) -> 403 Forbidden (Quiz.AccessDenied)
        var bobFullRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/quizzes/{quizId}?safe=false");
        Assert.Equal(HttpStatusCode.Forbidden, bobFullRes.StatusCode);

        // 3. Alice (creator & workspace owner) requests full quiz (safe=false) -> 200 OK with answer key
        var aliceFullRes = await clientAlice.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/quizzes/{quizId}?safe=false");
        Assert.Equal(HttpStatusCode.OK, aliceFullRes.StatusCode);
        var fullQuiz = await aliceFullRes.Content.ReadFromJsonAsync<QuizDetailDto>();
        Assert.NotNull(fullQuiz);
        Assert.Equal("TopSecretAnswer", fullQuiz.Questions[0].CorrectAnswer);
    }

    [Fact]
    public async Task StudyEndpoints_CrossUserIsolation_InSharedWorkspace()
    {
        var (clientAlice, userAlice, workspaceAlice) = await CreateUserAndWorkspaceAsync("iso_a");
        var (clientBob, userBob, _) = await CreateUserAndWorkspaceAsync("iso_b");

        // 1. Alice creates topic and flashcard
        var createTopicRes = await clientAlice.PostAsJsonAsync(
            $"/api/workspaces/{workspaceAlice.Id}/study/topics",
            new CreateStudyTopicRequest("Alice Architecture", "Microservices"));
        Assert.Equal(HttpStatusCode.OK, createTopicRes.StatusCode);
        var topic = await createTopicRes.Content.ReadFromJsonAsync<StudyTopicDto>();
        Assert.NotNull(topic);

        var createCardRes = await clientAlice.PostAsJsonAsync(
            $"/api/workspaces/{workspaceAlice.Id}/study/flashcards?topicId={topic.Id}",
            new CreateFlashcardRequest("What is SAGAs?", "Distributed transactions", "Medium", null, null, null));
        Assert.Equal(HttpStatusCode.OK, createCardRes.StatusCode);
        var card = await createCardRes.Content.ReadFromJsonAsync<FlashcardDto>();
        Assert.NotNull(card);

        // 2. Add Bob as a member to Alice's workspace
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceAlice.Id,
                UserId = userBob.UserId,
                Role = WorkspaceRole.Viewer
            });
            await db.SaveChangesAsync();
        }

        // 3. Bob queries Alice's workspace endpoints
        // Topics list should be empty for Bob
        var bobTopicsRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics");
        Assert.Equal(HttpStatusCode.OK, bobTopicsRes.StatusCode);
        var bobTopics = await bobTopicsRes.Content.ReadFromJsonAsync<List<StudyTopicDto>>();
        Assert.NotNull(bobTopics);
        Assert.Empty(bobTopics);

        // Bob cannot get Alice's topic by ID
        var bobGetTopicRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics/{topic.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bobGetTopicRes.StatusCode);

        // Bob cannot update Alice's topic
        var bobUpdateTopicRes = await clientBob.PutAsJsonAsync(
            $"/api/workspaces/{workspaceAlice.Id}/study/topics/{topic.Id}",
            new UpdateStudyTopicRequest("Bob Hijack", "None"));
        Assert.Equal(HttpStatusCode.NotFound, bobUpdateTopicRes.StatusCode);

        // Bob cannot delete Alice's topic
        var bobDeleteTopicRes = await clientBob.DeleteAsync($"/api/workspaces/{workspaceAlice.Id}/study/topics/{topic.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bobDeleteTopicRes.StatusCode);

        // Bob cannot view Alice's flashcards
        var bobCardsRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/flashcards");
        Assert.Equal(HttpStatusCode.OK, bobCardsRes.StatusCode);
        var bobCards = await bobCardsRes.Content.ReadFromJsonAsync<List<FlashcardDto>>();
        Assert.NotNull(bobCards);
        Assert.Empty(bobCards);

        // Bob's dashboard shows 0 topics, 0 flashcards
        var bobDashRes = await clientBob.GetAsync($"/api/workspaces/{workspaceAlice.Id}/study/dashboard");
        Assert.Equal(HttpStatusCode.OK, bobDashRes.StatusCode);
        var bobDash = await bobDashRes.Content.ReadFromJsonAsync<StudyDashboardDto>();
        Assert.NotNull(bobDash);
        Assert.Equal(0, bobDash.TotalTopics);
        Assert.Equal(0, bobDash.TotalFlashcards);
        Assert.Equal(0, bobDash.TotalQuizzes);
    }
}
