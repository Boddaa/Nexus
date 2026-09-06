namespace Nexus.Application.Common.Options;

public class VisualThinkingOptions
{
    public const string SectionName = "VisualThinking";

    public int MaxBoardsPerWorkspace { get; set; } = 100;
    public int MaxBoardItemsPerBoard { get; set; } = 2000;
    public int MaxMindMapsPerWorkspace { get; set; } = 100;
    public int MaxNodesPerMindMap { get; set; } = 1000;
    public int MaxEdgesPerMindMap { get; set; } = 2000;
    public int MaxBatchItems { get; set; } = 100;
    public int MaxBatchNodes { get; set; } = 200;
    public int MaxTitleLength { get; set; } = 200;
    public int MaxLabelLength { get; set; } = 500;
}
