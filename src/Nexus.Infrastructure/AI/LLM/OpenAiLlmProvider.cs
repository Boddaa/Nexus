using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.LLM;

public class OpenAiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    public string ProviderName => "OpenAI";

    public OpenAiLlmProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LLMResponse> ChatAsync(
        LLMRequest request,
        LlmOptions options,
        CancellationToken cancellationToken = default)
    {
        var apiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new LlmConfigurationException(
                "OpenAI API key is missing. Please configure 'AI:LLM:ApiKey' or set the 'OPENAI_API_KEY' environment variable.");
        }

        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "https://api.openai.com/v1/chat/completions"
            : options.Endpoint.TrimEnd('/') + (options.Endpoint.EndsWith("/chat/completions") ? "" : "/v1/chat/completions");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? (string.IsNullOrWhiteSpace(options.Model) ? "gpt-4o-mini" : options.Model)
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
            temperature = request.Temperature ?? options.Temperature,
            max_tokens = request.MaxTokens ?? options.MaxTokens
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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
            throw new LlmException($"Failed to connect to OpenAI endpoint at '{endpoint}': {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LlmException($"OpenAI API request failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
        }

        OpenAiChatResponse? chatResponse;
        try
        {
            chatResponse = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new LlmException($"Failed to deserialize OpenAI response: {ex.Message}", ex);
        }

        if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0 || chatResponse.Choices[0].Message == null)
        {
            throw new LlmException("OpenAI returned an empty or invalid response.");
        }

        var content = chatResponse.Choices[0].Message!.Content ?? string.Empty;

        return new LLMResponse(
            Content: content,
            Model: chatResponse.Model ?? model,
            PromptTokens: chatResponse.Usage?.PromptTokens,
            CompletionTokens: chatResponse.Usage?.CompletionTokens,
            TotalTokens: chatResponse.Usage?.TotalTokens);
    }

    private class OpenAiChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; set; }
    }

    private class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; set; }
    }

    private class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
