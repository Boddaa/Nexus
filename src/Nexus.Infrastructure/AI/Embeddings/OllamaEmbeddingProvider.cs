using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.Embeddings;

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    public string ProviderName => "Ollama";

    public OllamaEmbeddingProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingOptions options,
        CancellationToken cancellationToken = default)
    {
        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "http://localhost:11434/api/embeddings"
            : options.Endpoint.TrimEnd('/') + "/api/embeddings";

        var model = string.IsNullOrWhiteSpace(options.Model)
            ? "nomic-embed-text"
            : options.Model;

        var requestBody = new
        {
            model = model,
            prompt = text
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new EmbeddingException($"Failed to connect to Ollama embedding endpoint: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new EmbeddingException(
                $"Ollama embedding request failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
        }

        var responseData = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken);
        if (responseData?.Embedding == null || responseData.Embedding.Length == 0)
        {
            throw new EmbeddingException("Ollama returned an empty embedding vector.");
        }

        return responseData.Embedding;
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        EmbeddingOptions options,
        CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>(texts.Count);
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, options, cancellationToken);
            results.Add(embedding);
        }
        return results;
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
