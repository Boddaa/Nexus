namespace Nexus.Application.Common.Options;

public class RagOptions
{
    public const string SectionName = "AI:RAG";

    public string RetrievalMode { get; set; } = "Hybrid"; // "Keyword", "Semantic", "Hybrid"
    public int TopK { get; set; } = 8;
    public int MaxSources { get; set; } = 8;
    public int MaxContextCharacters { get; set; } = 6000;
    public int MaxConversationMessages { get; set; } = 10;
    /// <summary>
    /// Minimum normalized relevance score in range [0.0, 1.0].
    /// Sources with normalized relevance below this threshold will be filtered out.
    /// Default is 0.05 (5% relevance).
    /// </summary>
    public double MinimumRelevanceScore { get; set; } = 0.05;
}
