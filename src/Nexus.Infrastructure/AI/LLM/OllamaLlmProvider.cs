using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.LLM;

public class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    public string ProviderName => "Ollama";

    public OllamaLlmProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LLMResponse> ChatAsync(
        LLMRequest request,
        LlmOptions options,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "http://localhost:11434"
            : options.Endpoint.TrimEnd('/');

        var endpoint = baseUrl.EndsWith("/api/chat") ? baseUrl : $"{baseUrl}/api/chat";

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? (string.IsNullOrWhiteSpace(options.Model) ? "llama3" : options.Model)
            : request.Model;

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }

        foreach (var msg in request.Messages)
        {
            messages.Add(new { role = msg.Role.ToLowerInvariant(), content = msg.Content });
        }

        var requestBody = new
        {
            model = model,
            messages = messages,
            stream = false,
            options = new
            {
                temperature = request.Temperature ?? options.Temperature
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = JsonContent.Create(requestBody);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LlmException($"Failed to connect to Ollama endpoint at '{endpoint}': {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LlmException($"Ollama API request failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
        }

        OllamaChatResponse? chatResponse;
        try
        {
            chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new LlmException($"Failed to deserialize Ollama response: {ex.Message}", ex);
        }

        if (chatResponse?.Message == null)
        {
            throw new LlmException("Ollama returned an empty or invalid response.");
        }

        var content = chatResponse.Message.Content ?? string.Empty;

        return new LLMResponse(
            Content: content,
            Model: chatResponse.Model ?? model,
            PromptTokens: chatResponse.PromptEvalCount,
            CompletionTokens: chatResponse.EvalCount,
            TotalTokens: (chatResponse.PromptEvalCount ?? 0) + (chatResponse.EvalCount ?? 0));
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
