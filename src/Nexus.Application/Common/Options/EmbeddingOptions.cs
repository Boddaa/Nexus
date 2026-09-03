namespace Nexus.Application.Common.Options;

public class EmbeddingOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>
    /// The embedding provider name, e.g. "OpenAI", "Ollama", or "None".
    /// </summary>
    public string Provider { get; set; } = "None";

    /// <summary>
    /// The embedding model identifier, e.g. "text-embedding-3-small" or "nomic-embed-text".
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The dimension vector size, e.g. 1536 for OpenAI text-embedding-3-small.
    /// </summary>
    public int Dimensions { get; set; } = 1536;

    /// <summary>
    /// Optional API key for remote providers.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional custom base URL or endpoint (e.g. for Ollama or Azure OpenAI).
    /// </summary>
    public string? Endpoint { get; set; }
}
