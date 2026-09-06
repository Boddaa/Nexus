namespace Nexus.Application.Features.AI.Services;

public interface IPromptBuilder
{
    string BuildSystemPrompt();
    string BuildUserPrompt(string question, string context);
}
