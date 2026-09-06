namespace Nexus.Application.Common.Options;

public class AiKnowledgeOptions
{
    public const string SectionName = "AI:Knowledge";

    public int MaxContextCharacters { get; set; } = 6000;
    public int MaxQuestionCount { get; set; } = 20;
    public int MaxInstructionCharacters { get; set; } = 2000;
    public bool EnableAutomaticProcessing { get; set; } = false;
    public bool EnableTitleGeneration { get; set; } = true;
    public int MaxTitleLength { get; set; } = 60;
}
