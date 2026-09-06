using Nexus.Application.DTOs.Conversations;

namespace Nexus.Application.Features.AI.Services;

public interface IContextBuilder
{
    string BuildContext(IReadOnlyList<ChatSourceDto> sources, int maxCharacters = 6000);
}
