namespace Nexus.Application.Common.Options;

public class ChunkingOptions
{
    public const string SectionName = "Chunking";

    /// <summary>
    /// Target chunk size in characters. Defaults to 1000.
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between consecutive chunks in characters. Defaults to 150.
    /// </summary>
    public int ChunkOverlap { get; set; } = 150;
}
