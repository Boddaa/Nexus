using Nexus.Application.Common.Models;
using Nexus.Application.Common.Options;

namespace Nexus.Infrastructure.AI.LLM;

public interface ILlmProvider
{
    string ProviderName { get; }

    Task<LLMResponse> ChatAsync(
        LLMRequest request,
        LlmOptions options,
        CancellationToken cancellationToken = default);
}
