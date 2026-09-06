using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Notes;
using Nexus.Application.DTOs.Pages;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Persistence;
using Xunit;

namespace Nexus.API.Tests;

public class AiMockWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory:AiApiTests_" + Guid.NewGuid().ToString("N"));

        builder.ConfigureServices(services =>
        {
            // Replace ILLMService with a test mock that returns valid answers
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILLMService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            services.AddScoped<ILLMService, TestApiLlmService>();
        });
    }

    public class TestApiLlmService : ILLMService
    {
        public Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            var userMsg = request.Messages.LastOrDefault()?.Content ?? "";
            var sysMsg = request.SystemPrompt ?? "";
            string content;

            if (sysMsg.Contains("question", StringComparison.OrdinalIgnoreCase) ||
                userMsg.Contains("question", StringComparison.OrdinalIgnoreCase) ||
                userMsg.Contains("Include answers", StringComparison.OrdinalIgnoreCase))
            {
                content = """
                [
                  {
                    "question": "What is clean code?",
                    "difficulty": "Beginner",
                    "answer": "Readable and maintainable code."
                  }
                ]
                """;
            }
            else if (sysMsg.Contains("key points", StringComparison.OrdinalIgnoreCase) ||
                     userMsg.Contains("key points", StringComparison.OrdinalIgnoreCase))
            {
                content = "• First key point\n• Second key point\n• Third key point";
            }
            else
            {
                content = "Mock LLM response for: " + userMsg.Substring(0, Math.Min(40, userMsg.Length));
            }

            return Task.FromResult(new LLMResponse(content, "mock-model", 40, 20, 60));
        }
    }
}

public class AiKnowledgeEndpointsTests : IClassFixture<AiMockWebApplicationFactory>
{
    private readonly AiMockWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AiKnowledgeEndpointsTests(AiMockWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient client, AuthResponse user, WorkspaceSummaryDto workspace)> CreateUserAndWorkspaceAsync(string prefix)
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
    public async Task AiEndpoints_WithoutAuthentication_Should_Return_401()
    {
        var dummyId = Guid.NewGuid();
        var req = new AiKnowledgeRequest("Document", dummyId);

        var resp1 = await _client.PostAsJsonAsync($"/api/workspaces/{dummyId}/ai/summarize", req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp1.StatusCode);

        var resp2 = await _client.PostAsJsonAsync($"/api/workspaces/{dummyId}/ai/explain", req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp2.StatusCode);

        var resp3 = await _client.GetAsync($"/api/workspaces/{dummyId}/ai/generations");
        Assert.Equal(HttpStatusCode.Unauthorized, resp3.StatusCode);
    }

    [Fact]
    public async Task Summarize_ValidDocument_Returns200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_summarize");

        // Seed a document with chunk in DbContext
        Guid docId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var doc = new Document
            {
                WorkspaceId = workspace.Id,
                FileName = "Doc1.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 100,
                Title = "Doc 1",
                Status = Domain.Enums.DocumentStatus.Processed,
                ExtractedText = "Document text content"
            };
            db.Documents.Add(doc);
            db.SaveChanges();

            db.DocumentChunks.Add(new DocumentChunk
            {
                DocumentId = doc.Id,
                WorkspaceId = workspace.Id,
                ChunkIndex = 0,
                Text = "Chunk 0 content",
                StartPosition = 0,
                EndPosition = 15
            });
            db.SaveChanges();
            docId = doc.Id;
        }

        var request = new AiKnowledgeRequest("Document", docId, "Summarize briefly");
        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/summarize", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiOperationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("Summarize", result.Operation);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task Explain_ValidNote_Returns200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_explain");

        // Create a Note
        var noteRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/notes", new CreateNoteRequest(
            "Polymorphism Note", "Polymorphism allows treating derived types through base interfaces.", "markdown"));
        noteRes.EnsureSuccessStatusCode();
        var note = (await noteRes.Content.ReadFromJsonAsync<NoteDto>())!;

        var request = new AiKnowledgeRequest("Note", note.Id, "Explain for a junior engineer");
        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/explain", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiOperationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("Explain", result.Operation);
    }

    [Fact]
    public async Task ExtractKeyPoints_ValidNote_Returns200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_kp");

        var noteRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/notes", new CreateNoteRequest(
            "Key Points Note", "Points to extract", "markdown"));
        noteRes.EnsureSuccessStatusCode();
        var note = (await noteRes.Content.ReadFromJsonAsync<NoteDto>())!;

        var request = new AiKnowledgeRequest("Note", note.Id);
        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/key-points", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiOperationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("KeyPoints", result.Operation);
        Assert.NotNull(result.KeyPoints);
        Assert.NotEmpty(result.KeyPoints);
    }

    [Fact]
    public async Task GenerateQuestions_ValidNote_Returns200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_questions");

        var noteRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/notes", new CreateNoteRequest(
            "Questions Note", "Test content", "markdown"));
        noteRes.EnsureSuccessStatusCode();
        var note = (await noteRes.Content.ReadFromJsonAsync<NoteDto>())!;

        var request = new GenerateQuestionsRequest("Note", note.Id, 2, "Beginner", true);
        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/questions", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiOperationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("Questions", result.Operation);
        Assert.NotNull(result.Questions);
        Assert.NotEmpty(result.Questions);
        Assert.Equal("What is clean code?", result.Questions[0].Question);
    }

    [Fact]
    public async Task GenerateStudyMaterial_ValidPage_Returns200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_study");

        var pageRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/pages", new CreatePageRequest(
            "Algorithms Page", "📄", null, "Binary search, dynamic programming", null, 0));
        pageRes.EnsureSuccessStatusCode();
        var page = (await pageRes.Content.ReadFromJsonAsync<PageDto>())!;

        var request = new GenerateStudyMaterialRequest("Page", page.Id);
        var response = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/study-material", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiOperationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("StudyMaterial", result.Operation);
    }

    [Fact]
    public async Task SaveAsNote_Flow_Returns200_AndCreatesNote()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_save_note");

        // 1. Create a Page
        var pageRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/pages", new CreatePageRequest(
            "Notes Container Page", "📁", null, "{}", null, 0));
        pageRes.EnsureSuccessStatusCode();
        var page = (await pageRes.Content.ReadFromJsonAsync<PageDto>())!;

        // 2. Run an AI operation to get a Generation ID
        var noteRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/notes", new CreateNoteRequest(
            "Source Note", "Important AI knowledge", "markdown", false, page.Id));
        noteRes.EnsureSuccessStatusCode();
        var note = (await noteRes.Content.ReadFromJsonAsync<NoteDto>())!;

        var aiOpRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/summarize", new AiKnowledgeRequest("Note", note.Id));
        aiOpRes.EnsureSuccessStatusCode();
        var aiResult = (await aiOpRes.Content.ReadFromJsonAsync<AiOperationResultDto>())!;

        // 3. Save as Note
        var saveReq = new SaveAiOutputAsNoteRequest(aiResult.Id, page.Id, "Saved Summary Note");
        var saveRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/save-as-note", saveReq);

        Assert.Equal(HttpStatusCode.OK, saveRes.StatusCode);
        var createdNote = await saveRes.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(createdNote);
        Assert.Equal("Saved Summary Note", createdNote.Title);
        Assert.Equal(page.Id, createdNote.PageId);
    }

    [Fact]
    public async Task GetGenerations_And_DeleteGeneration_Returns200()
    {
        var (client, _, workspace) = await CreateUserAndWorkspaceAsync("ai_history");

        var pageRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/pages", new CreatePageRequest(
            "History Source Page", "📄", null, "Content", null, 0));
        var page = (await pageRes.Content.ReadFromJsonAsync<PageDto>())!;

        // Generate something
        var aiRes = await client.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/ai/summarize", new AiKnowledgeRequest("Page", page.Id));
        var gen = (await aiRes.Content.ReadFromJsonAsync<AiOperationResultDto>())!;

        // Get History list
        var listRes = await client.GetAsync($"/api/workspaces/{workspace.Id}/ai/generations");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<AiGenerationSummaryDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, g => g.Id == gen.Id);

        // Get single generation
        var singleRes = await client.GetAsync($"/api/workspaces/{workspace.Id}/ai/generations/{gen.Id}");
        Assert.Equal(HttpStatusCode.OK, singleRes.StatusCode);

        // Delete generation
        var delRes = await client.DeleteAsync($"/api/workspaces/{workspace.Id}/ai/generations/{gen.Id}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
    }
}
