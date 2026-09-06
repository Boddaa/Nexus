namespace Nexus.Application.Common.Options;

public class LlmOptions
{
    public const string SectionName = "AI:LLM";

    public string Provider { get; set; } = "None";
    public string Model { get; set; } = "gpt-4o-mini";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 1500;
    public int TimeoutSeconds { get; set; } = 60;
}
