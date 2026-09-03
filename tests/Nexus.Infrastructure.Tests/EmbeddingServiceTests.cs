using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Infrastructure.AI.Embeddings;
using Xunit;

namespace Nexus.Infrastructure.Tests;

public class EmbeddingServiceTests
{
    #region Helper Mock HTTP Handler

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    #endregion

    #region DI & Configuration Tests

    [Fact]
    public void DI_Resolves_IEmbeddingService_Successfully()
    {
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=dummy;Database=dummy;",
            ["Embeddings:Provider"] = "None",
            ["Embeddings:Dimensions"] = "1536"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var embeddingService = serviceProvider.GetService<IEmbeddingService>();

        Assert.NotNull(embeddingService);
        Assert.IsType<EmbeddingService>(embeddingService);
        Assert.Equal(1536, embeddingService.EmbeddingDimension);
    }

    [Fact]
    public void Configuration_Is_Correctly_Loaded_From_Section()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Embeddings:Provider"] = "OpenAI",
            ["Embeddings:Model"] = "text-embedding-3-small",
            ["Embeddings:Dimensions"] = "1536",
            ["Embeddings:ApiKey"] = "sk-test-secret-key",
            ["Embeddings:Endpoint"] = "https://custom.openai.endpoint"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var services = new ServiceCollection();
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;

        Assert.Equal("OpenAI", options.Provider);
        Assert.Equal("text-embedding-3-small", options.Model);
        Assert.Equal(1536, options.Dimensions);
        Assert.Equal("sk-test-secret-key", options.ApiKey);
        Assert.Equal("https://custom.openai.endpoint", options.Endpoint);
    }

    #endregion

    #region Configuration Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EmbeddingService_When_Dimensions_Less_Than_Or_Equal_To_Zero_Throws_EmbeddingConfigurationException(int invalidDimensions)
    {
        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            Dimensions = invalidDimensions
        };

        var ex = Assert.Throws<EmbeddingConfigurationException>(() =>
            EmbeddingService.ValidateConfiguration(options));

        Assert.Contains("dimension must be greater than 0", ex.Message);
    }

    [Fact]
    public void EmbeddingService_When_Model_Empty_With_Provider_Throws_EmbeddingConfigurationException()
    {
        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "   ",
            Dimensions = 1536
        };

        var ex = Assert.Throws<EmbeddingConfigurationException>(() =>
            EmbeddingService.ValidateConfiguration(options));

        Assert.Contains("Embedding model must be specified", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    public async Task EmbeddingService_When_Provider_Is_None_Or_Empty_Throws_EmbeddingConfigurationException(string provider)
    {
        var options = Options.Create(new EmbeddingOptions
        {
            Provider = provider,
            Dimensions = 1536
        });

        var service = new EmbeddingService(options, Array.Empty<IEmbeddingProvider>(), NullLogger<EmbeddingService>.Instance);

        var ex = await Assert.ThrowsAsync<EmbeddingConfigurationException>(() =>
            service.GenerateEmbeddingAsync("Some text to embed"));

        Assert.Contains("No embedding provider is configured", ex.Message);
    }

    [Fact]
    public async Task EmbeddingService_When_Unknown_Provider_Throws_EmbeddingConfigurationException()
    {
        var options = Options.Create(new EmbeddingOptions
        {
            Provider = "UnsupportedProvider",
            Model = "unsupported-model",
            Dimensions = 768
        });

        var service = new EmbeddingService(options, Array.Empty<IEmbeddingProvider>(), NullLogger<EmbeddingService>.Instance);

        var ex = await Assert.ThrowsAsync<EmbeddingConfigurationException>(() =>
            service.GenerateEmbeddingAsync("Some text"));

        Assert.Contains("Configured embedding provider 'UnsupportedProvider' is not supported", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmbeddingService_Null_Or_Whitespace_Text_Throws_ArgumentException(string? invalidText)
    {
        var options = Options.Create(new EmbeddingOptions
        {
            Provider = "None"
        });

        var service = new EmbeddingService(options, Array.Empty<IEmbeddingProvider>(), NullLogger<EmbeddingService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateEmbeddingAsync(invalidText!));
    }

    [Fact]
    public async Task EmbeddingService_Batch_With_Empty_List_Returns_Empty()
    {
        var options = Options.Create(new EmbeddingOptions { Provider = "None" });
        var service = new EmbeddingService(options, Array.Empty<IEmbeddingProvider>(), NullLogger<EmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsAsync(Array.Empty<string>());

        Assert.Empty(result);
    }

    #endregion

    #region OpenAI Provider Tests (Isolated Unit Tests without external network)

    [Fact]
    public async Task OpenAiEmbeddingProvider_Missing_ApiKey_Throws_EmbeddingConfigurationException()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var provider = new OpenAiEmbeddingProvider(httpClient);

        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            ApiKey = "" // Missing
        };

        // Temporarily clear environment variable if set
        var existingEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            var ex = await Assert.ThrowsAsync<EmbeddingConfigurationException>(() =>
                provider.GenerateEmbeddingAsync("Text", options));

            Assert.Contains("OpenAI API key is missing", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", existingEnv);
        }
    }

    [Fact]
    public async Task OpenAiEmbeddingProvider_Successful_Http_Response_Returns_Embedding()
    {
        var expectedVector = new float[] { 0.05f, -0.12f, 0.88f };
        var jsonResponse = JsonSerializer.Serialize(new
        {
            @object = "list",
            data = new[]
            {
                new { @object = "embedding", index = 0, embedding = expectedVector }
            }
        });

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://api.openai.com/v1/embeddings", req.RequestUri!.ToString());
            Assert.Equal("Bearer sk-valid-key", req.Headers.Authorization!.ToString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var provider = new OpenAiEmbeddingProvider(httpClient);

        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            ApiKey = "sk-valid-key"
        };

        var result = await provider.GenerateEmbeddingAsync("Hello world", options);

        Assert.NotNull(result);
        Assert.Equal(expectedVector, result);
    }

    [Fact]
    public async Task OpenAiEmbeddingProvider_Batch_Successful_Http_Response_Returns_Ordered_Embeddings()
    {
        var vec1 = new float[] { 0.1f, 0.2f };
        var vec2 = new float[] { 0.3f, 0.4f };

        // Deliberately return them out of order (index 1 first) to verify sorting by index
        var jsonResponse = JsonSerializer.Serialize(new
        {
            @object = "list",
            data = new[]
            {
                new { @object = "embedding", index = 1, embedding = vec2 },
                new { @object = "embedding", index = 0, embedding = vec1 }
            }
        });

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var provider = new OpenAiEmbeddingProvider(httpClient);

        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            ApiKey = "sk-valid-key"
        };

        var results = await provider.GenerateEmbeddingsAsync(new[] { "First", "Second" }, options);

        Assert.Equal(2, results.Count);
        Assert.Equal(vec1, results[0]);
        Assert.Equal(vec2, results[1]);
    }

    [Fact]
    public async Task OpenAiEmbeddingProvider_Http_Error_Throws_EmbeddingException_Without_Swallowing()
    {
        var errorResponse = "{\"error\": {\"message\": \"Invalid API key\", \"type\": \"invalid_request_error\"}}";

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(errorResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var provider = new OpenAiEmbeddingProvider(httpClient);

        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            ApiKey = "sk-invalid-key"
        };

        var ex = await Assert.ThrowsAsync<EmbeddingException>(() =>
            provider.GenerateEmbeddingAsync("Test", options));

        Assert.Contains("401", ex.Message);
        Assert.Contains("Invalid API key", ex.Message);
    }

    #endregion

    #region Ollama Provider Tests

    [Fact]
    public async Task OllamaEmbeddingProvider_Successful_Http_Response_Returns_Embedding()
    {
        var expectedVector = new float[] { 0.33f, 0.66f, 0.99f };
        var jsonResponse = JsonSerializer.Serialize(new
        {
            embedding = expectedVector
        });

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/api/embeddings", req.RequestUri!.ToString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var provider = new OllamaEmbeddingProvider(httpClient);

        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Model = "nomic-embed-text",
            Endpoint = "http://localhost:11434"
        };

        var result = await provider.GenerateEmbeddingAsync("Local query", options);

        Assert.NotNull(result);
        Assert.Equal(expectedVector, result);
    }

    [Fact]
    public async Task OllamaEmbeddingProvider_Http_Error_Throws_EmbeddingException()
    {
        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("model not found", Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(mockHandler);
        var provider = new OllamaEmbeddingProvider(httpClient);

        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Model = "nonexistent-model"
        };

        var ex = await Assert.ThrowsAsync<EmbeddingException>(() =>
            provider.GenerateEmbeddingAsync("Local query", options));

        Assert.Contains("500", ex.Message);
        Assert.Contains("model not found", ex.Message);
    }

    #endregion
}
