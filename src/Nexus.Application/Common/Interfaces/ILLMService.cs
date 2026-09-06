using Nexus.Application.Common.Models;

namespace Nexus.Application.Common.Interfaces;

public interface ILLMService
{
    Task<LLMResponse> ChatAsync(LLMRequest request, CancellationToken cancellationToken = default);
}
