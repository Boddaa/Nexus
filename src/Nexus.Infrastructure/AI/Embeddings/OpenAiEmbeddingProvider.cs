using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.Embeddings;

public class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    public string ProviderName => "OpenAI";

    public OpenAiEmbeddingProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingOptions options,
        CancellationToken cancellationToken = default)
    {
        var results = await GenerateEmbeddingsAsync(new[] { text }, options, cancellationToken);
        if (results.Count == 0)
        {
            throw new EmbeddingException("OpenAI returned no embeddings for the provided text.");
        }
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        EmbeddingOptions options,
        CancellationToken cancellationToken = default)
    {
        var apiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new EmbeddingConfigurationException(
                "OpenAI API key is missing. Please configure 'Embeddings:ApiKey' or set the 'OPENAI_API_KEY' environment variable.");
        }

        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "https://api.openai.com/v1/embeddings"
            : options.Endpoint.TrimEnd('/') + "/v1/embeddings";

        var model = string.IsNullOrWhiteSpace(options.Model)
            ? "text-embedding-3-small"
            : options.Model;

        var requestBody = new
        {
            input = texts,
            model = model
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new EmbeddingException($"Failed to connect to OpenAI embedding endpoint: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new EmbeddingException(
                $"OpenAI embedding request failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
        }

        var responseData = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(cancellationToken);
        if (responseData?.Data == null || responseData.Data.Count == 0)
        {
            throw new EmbeddingException("OpenAI returned an empty embedding response.");
        }

        var sortedData = responseData.Data.OrderBy(d => d.Index).ToList();
        var embeddings = new List<float[]>(sortedData.Count);

        foreach (var item in sortedData)
        {
            if (item.Embedding == null || item.Embedding.Length == 0)
            {
                throw new EmbeddingException($"OpenAI returned an empty embedding array at index {item.Index}.");
            }
            embeddings.Add(item.Embedding);
        }

        return embeddings;
    }

    private class OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiEmbeddingItem>? Data { get; set; }
    }

    private class OpenAiEmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
