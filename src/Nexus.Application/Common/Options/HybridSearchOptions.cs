namespace Nexus.Application.Common.Options;

public class HybridSearchOptions
{
    public const string SectionName = "Search";

    /// <summary>
    /// Weight applied to normalized keyword search scores in hybrid mode. Defaults to 0.5.
    /// </summary>
    public double KeywordWeight { get; set; } = 0.5;

    /// <summary>
    /// Weight applied to vector similarity scores in hybrid mode. Defaults to 0.5.
    /// </summary>
    public double VectorWeight { get; set; } = 0.5;

    /// <summary>
    /// Default Top-K results for semantic vector search. Defaults to 10.
    /// </summary>
    public int DefaultTopK { get; set; } = 10;

    /// <summary>
    /// Maximum allowed Top-K results for semantic vector search. Defaults to 50.
    /// </summary>
    public int MaxTopK { get; set; } = 50;
}
