namespace Nexus.Application.Common.Options;

public class RagOptions
{
    public const string SectionName = "AI:RAG";

    public string RetrievalMode { get; set; } = "Hybrid"; // "Keyword", "Semantic", "Hybrid"
    public int TopK { get; set; } = 8;
    public int MaxSources { get; set; } = 8;
    public int MaxContextCharacters { get; set; } = 6000;
    public int MaxConversationMessages { get; set; } = 10;
    public double MinimumRelevanceScore { get; set; } = 0.05;
}
