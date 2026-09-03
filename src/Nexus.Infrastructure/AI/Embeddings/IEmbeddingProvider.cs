using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.Embeddings;

public interface IEmbeddingProvider
{
    string ProviderName { get; }

    Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        EmbeddingOptions options,
        CancellationToken cancellationToken = default);
}
