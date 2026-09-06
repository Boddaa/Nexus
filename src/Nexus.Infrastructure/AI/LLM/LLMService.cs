using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Exceptions;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.LLM;

public class LLMService : ILLMService
{
    private readonly IEnumerable<ILlmProvider> _providers;
    private readonly IOptions<LlmOptions> _options;
    private readonly ILogger<LLMService> _logger;

    public LLMService(
        IEnumerable<ILlmProvider> providers,
        IOptions<LlmOptions> options,
        ILogger<LLMService> logger)
    {
        _providers = providers;
        _options = options;
        _logger = logger;
    }

    public async Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var options = _options.Value ?? new LlmOptions();

        if (string.IsNullOrWhiteSpace(options.Provider) ||
            options.Provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            throw new LlmConfigurationException(
                "No LLM provider configured. Please configure 'AI:LLM:Provider' with 'OpenAI' or 'Ollama' and supply necessary credentials.");
        }

        var provider = _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(options.Provider, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            var available = string.Join(", ", _providers.Select(p => p.ProviderName));
            throw new LlmConfigurationException(
                $"Configured LLM provider '{options.Provider}' is not supported. Available providers: {available}");
        }

        _logger.LogInformation("Executing LLM chat completion using provider '{Provider}' and model '{Model}'",
            provider.ProviderName, request.Model ?? options.Model);

        return await provider.ChatAsync(request, options, cancellationToken);
    }
}
