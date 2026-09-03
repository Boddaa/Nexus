using System.Collections.Concurrent;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Enums;

namespace Nexus.Infrastructure.AI;

public class MockChatService : IChatService
{
    public Task<ChatResponse> GetChatCompletionAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var lastMessage = request.Messages.LastOrDefault()?.Content ?? "Hello";
        var responseText = $"[Nexus AI]: I analyzed your knowledge workspace regarding '{lastMessage}'. This is an intelligent response grounded in your workspace.";
        return Task.FromResult(new ChatResponse(responseText, 50, 120));
    }
}


public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, (ReadOnlyMemory<float> Vector, IDictionary<string, object> Metadata)> _store = new();

    public Task<string> UpsertVectorAsync(string vectorId, ReadOnlyMemory<float> embedding, IDictionary<string, object> metadata, CancellationToken cancellationToken = default)
    {
        _store[vectorId] = (embedding, metadata);
        return Task.FromResult(vectorId);
    }

    public Task<IReadOnlyList<(string VectorId, double Score, IDictionary<string, object> Metadata)>> SearchSimilarAsync(
        ReadOnlyMemory<float> queryEmbedding,
        Guid workspaceId,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var results = new List<(string VectorId, double Score, IDictionary<string, object> Metadata)>();

        foreach (var kvp in _store)
        {
            if (kvp.Value.Metadata.TryGetValue("WorkspaceId", out var wsIdObj) &&
                wsIdObj is string wsIdStr &&
                Guid.TryParse(wsIdStr, out var wsId) &&
                wsId == workspaceId)
            {
                // Cosine similarity
                var sim = CosineSimilarity(queryEmbedding.Span, kvp.Value.Vector.Span);
                results.Add((kvp.Key, sim, kvp.Value.Metadata));
            }
        }

        var sorted = results.OrderByDescending(r => r.Score).Take(topK).ToList();
        return Task.FromResult<IReadOnlyList<(string VectorId, double Score, IDictionary<string, object> Metadata)>>(sorted);
    }

    public Task<bool> DeleteVectorAsync(string vectorId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(vectorId, out _));
    }

    private static double CosineSimilarity(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        if (v1.Length != v2.Length || v1.IsEmpty) return 0.0;
        double dot = 0.0, norm1 = 0.0, norm2 = 0.0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            norm1 += v1[i] * v1[i];
            norm2 += v2[i] * v2[i];
        }
        if (norm1 == 0 || norm2 == 0) return 0.0;
        return dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }
}

public class MockRagService : IRagService
{
    private readonly IChatService _chatService;

    public MockRagService(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task<RagResponse> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default)
    {
        var chatResp = await _chatService.GetChatCompletionAsync(new ChatRequest(
            new[] { new ChatMessage(AiRole.User, request.Query) }
        ), cancellationToken);

        var citations = new List<SourceReferenceDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), null, null, "Workspace Knowledge Base", "Relevant concept excerpt matching: " + request.Query, 0.92, 1)
        };

        return new RagResponse(chatResp.Content, citations, chatResp.PromptTokens, chatResp.CompletionTokens);
    }
}

public class MockAiDocumentAnalyzer : IAiDocumentAnalyzer
{
    public Task<string> SummarizeTextAsync(string text, int maxWords = 300, CancellationToken cancellationToken = default)
    {
        var preview = text.Length > 200 ? text[..200] + "..." : text;
        return Task.FromResult($"Summary: {preview}");
    }

    public Task<GeneratedMindMap> GenerateMindMapAsync(string text, string rootTopic, CancellationToken cancellationToken = default)
    {
        var rootId = "node-1";
        var nodes = new List<MindMapNodeDto>
        {
            new(rootId, rootTopic, "Main Topic", null, null, null),
            new("node-2", "Key Concept 1", "Detailed aspect of " + rootTopic, rootId, null, null),
            new("node-3", "Key Concept 2", "Secondary aspect of " + rootTopic, rootId, null, null)
        };
        var edges = new List<MindMapEdgeDto>
        {
            new(rootId, "node-2", "Includes", "Hierarchy"),
            new(rootId, "node-3", "Uses", "Hierarchy")
        };
        return Task.FromResult(new GeneratedMindMap(rootTopic + " Mind Map", nodes, edges));
    }
}

public class MockAiStudyService : IAiStudyService
{
    public Task<GeneratedQuiz> GenerateQuizAsync(string contextText, string topic, int questionCount = 5, string difficulty = "Medium", CancellationToken cancellationToken = default)
    {
        var questions = new List<QuizQuestionDto>
        {
            new($"What is the primary role of {topic}?", QuestionType.MultipleChoice, new[] { "Option A", "Option B", "Option C", "Option D" }, "Option A", "Option A is the correct architectural definition.")
        };
        return Task.FromResult(new GeneratedQuiz($"{topic} Practice Quiz", $"Assessing knowledge of {topic}", questions));
    }

    public Task<IReadOnlyList<GeneratedFlashcard>> GenerateFlashcardsAsync(string contextText, string topic, int cardCount = 5, CancellationToken cancellationToken = default)
    {
        var cards = new List<GeneratedFlashcard>
        {
            new($"What is {topic}?", $"{topic} is a core component within the NEXUS workspace.", topic)
        };
        return Task.FromResult<IReadOnlyList<GeneratedFlashcard>>(cards);
    }
}
