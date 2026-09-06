using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Study;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Enums;
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
}
