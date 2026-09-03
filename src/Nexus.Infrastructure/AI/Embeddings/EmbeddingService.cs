using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.Embeddings;

public class EmbeddingService : IEmbeddingService
{
    private readonly IOptions<EmbeddingOptions> _options;
    private readonly IEnumerable<IEmbeddingProvider> _providers;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IOptions<EmbeddingOptions> options,
        IEnumerable<IEmbeddingProvider> providers,
        ILogger<EmbeddingService> logger)
    {
        _options = options;
        _providers = providers;
        _logger = logger;

        ValidateConfiguration(_options.Value);
    }

    public int EmbeddingDimension => _options.Value.Dimensions;

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or whitespace for embedding generation.", nameof(text));
        }

        var provider = ResolveProvider();
        _logger.LogDebug("Generating embedding with provider '{Provider}' and model '{Model}'.", provider.ProviderName, _options.Value.Model);

        return await provider.GenerateEmbeddingAsync(text, _options.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        for (int i = 0; i < texts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i]))
            {
                throw new ArgumentException($"Text at index {i} cannot be null or whitespace for embedding generation.", nameof(texts));
            }
        }

        var provider = ResolveProvider();
        _logger.LogDebug("Generating {Count} embeddings with provider '{Provider}' and model '{Model}'.", texts.Count, provider.ProviderName, _options.Value.Model);

        return await provider.GenerateEmbeddingsAsync(texts, _options.Value, cancellationToken);
    }

    private IEmbeddingProvider ResolveProvider()
    {
        var providerName = _options.Value.Provider;

        if (string.IsNullOrWhiteSpace(providerName) || providerName.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            throw new EmbeddingConfigurationException(
                "No embedding provider is configured. Please specify a provider (e.g. 'OpenAI' or 'Ollama') and valid credentials in configuration under 'Embeddings'.");
        }

        var provider = _providers.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            var availableProviders = string.Join(", ", _providers.Select(p => $"'{p.ProviderName}'"));
            throw new EmbeddingConfigurationException(
                $"Configured embedding provider '{providerName}' is not supported. Available providers: {availableProviders}.");
        }

        return provider;
    }

    public static void ValidateConfiguration(EmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var providerName = options.Provider;

        // If a real provider is set, strictly validate model and dimensions
        if (!string.IsNullOrWhiteSpace(providerName) && !providerName.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Dimensions <= 0)
            {
                throw new EmbeddingConfigurationException(
                    $"Embedding dimension must be greater than 0. Found: {options.Dimensions}.");
            }

            if (string.IsNullOrWhiteSpace(options.Model))
            {
                throw new EmbeddingConfigurationException(
                    $"Embedding model must be specified when provider '{providerName}' is configured.");
            }
        }
    }
}
