using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;
using Nexus.Infrastructure.AI.LLM;
using Xunit;

namespace Nexus.Infrastructure.Tests;

public class LlmServiceTests
{
    private class FakeProvider : ILlmProvider
    {
        public string ProviderName => "TestProvider";

        public Task<LLMResponse> ChatAsync(LLMRequest request, LlmOptions options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMResponse("Response from " + ProviderName, options.Model, 10, 20, 30));
        }
    }

    [Fact]
    public async Task ChatAsync_NoProviderConfigured_ThrowsLlmConfigurationException()
    {
        var providers = new ILlmProvider[] { new FakeProvider() };
        var options = Options.Create(new LlmOptions { Provider = "None" });
        var service = new LLMService(providers, options, NullLogger<LLMService>.Instance);

        var request = new LLMRequest(new[] { new LLMChatMessage("user", "hi") });

        await Assert.ThrowsAsync<LlmConfigurationException>(() => service.ChatAsync(request));
    }

    [Fact]
    public async Task ChatAsync_UnknownProvider_ThrowsLlmConfigurationException()
    {
        var providers = new ILlmProvider[] { new FakeProvider() };
        var options = Options.Create(new LlmOptions { Provider = "UnsupportedProvider" });
        var service = new LLMService(providers, options, NullLogger<LLMService>.Instance);

        var request = new LLMRequest(new[] { new LLMChatMessage("user", "hi") });

        await Assert.ThrowsAsync<LlmConfigurationException>(() => service.ChatAsync(request));
    }

    [Fact]
    public async Task ChatAsync_ValidConfiguredProvider_ReturnsResponse()
    {
        var providers = new ILlmProvider[] { new FakeProvider() };
        var options = Options.Create(new LlmOptions { Provider = "TestProvider", Model = "custom-model" });
        var service = new LLMService(providers, options, NullLogger<LLMService>.Instance);

        var request = new LLMRequest(new[] { new LLMChatMessage("user", "Hello") });
        var response = await service.ChatAsync(request);

        Assert.NotNull(response);
        Assert.Equal("Response from TestProvider", response.Content);
        Assert.Equal("custom-model", response.Model);
    }

    [Fact]
    public async Task OpenAiProvider_MissingApiKey_ThrowsLlmConfigurationException()
    {
        using var client = new HttpClient();
        var provider = new OpenAiLlmProvider(client);
        var options = new LlmOptions { ApiKey = "", Provider = "OpenAI" };

        var request = new LLMRequest(new[] { new LLMChatMessage("user", "Hi") });

        // Ensure env var is not leaking into this specific test
        var oldVal = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            await Assert.ThrowsAsync<LlmConfigurationException>(() => provider.ChatAsync(request, options));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", oldVal);
        }
    }
}
