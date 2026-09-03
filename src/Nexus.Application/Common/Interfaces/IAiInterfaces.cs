using Nexus.Application.Common.Models;

namespace Nexus.Application.Common.Interfaces;

public interface IChatService
{
    Task<ChatResponse> GetChatCompletionAsync(ChatRequest request, CancellationToken cancellationToken = default);
}


public interface IVectorStore
{
    Task<string> UpsertVectorAsync(string vectorId, ReadOnlyMemory<float> embedding, IDictionary<string, object> metadata, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string VectorId, double Score, IDictionary<string, object> Metadata)>> SearchSimilarAsync(
        ReadOnlyMemory<float> queryEmbedding,
        Guid workspaceId,
        int topK = 5,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteVectorAsync(string vectorId, CancellationToken cancellationToken = default);
}

public interface IRagService
{
    Task<RagResponse> AnswerQuestionAsync(RagRequest request, CancellationToken cancellationToken = default);
}

public interface IAiDocumentAnalyzer
{
    Task<string> SummarizeTextAsync(string text, int maxWords = 300, CancellationToken cancellationToken = default);
    Task<GeneratedMindMap> GenerateMindMapAsync(string text, string rootTopic, CancellationToken cancellationToken = default);
}

public interface IAiStudyService
{
    Task<GeneratedQuiz> GenerateQuizAsync(string contextText, string topic, int questionCount = 5, string difficulty = "Medium", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeneratedFlashcard>> GenerateFlashcardsAsync(string contextText, string topic, int cardCount = 5, CancellationToken cancellationToken = default);
}

public interface IHybridSearchService
{
    Task<SearchResponse> HybridSearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
