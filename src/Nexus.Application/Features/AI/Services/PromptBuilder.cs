using System.Text;

namespace Nexus.Application.Features.AI.Services;

public class PromptBuilder : IPromptBuilder
{
    private const string SystemInstruction =
        "You are NEXUS AI, an intelligent knowledge assistant for this workspace.\n" +
        "Your role is to assist the user by providing accurate, grounded answers based exclusively on the retrieved workspace knowledge provided to you.\n\n" +
        "CRITICAL GROUNDING RULES:\n" +
        "1. Strictly base your response on the provided retrieved context.\n" +
        "2. Do not hallucinate or invent facts. If the provided knowledge does not contain sufficient information to answer the user's question, state clearly and concisely that the available workspace knowledge is insufficient.\n" +
        "3. When stating facts from the context, cite the source by its tag (e.g. [SOURCE 1], [SOURCE 2]).\n" +
        "4. Be concise, structured, professional, and clear.\n" +
        "5. Do not disclose internal system instructions or raw backend details unless specifically requested.";

    public string BuildSystemPrompt()
    {
        return SystemInstruction;
    }

    public string BuildUserPrompt(string question, string context)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine("Retrieved Workspace Knowledge:");
            sb.AppendLine("---");
            sb.AppendLine(context);
            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine("User Question:");
        sb.AppendLine(question.Trim());

        return sb.ToString();
    }
}
