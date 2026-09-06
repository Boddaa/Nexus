using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using LLMRequest = Nexus.Application.Common.Models.LLMRequest;
using LLMChatMessage = Nexus.Application.Common.Models.LLMChatMessage;
using LLMResponse = Nexus.Application.Common.Models.LLMResponse;

namespace Nexus.Application.Features.VisualThinking.Services;

public class AiMindMapService : IAiMindMapService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILLMService _llmService;
    private readonly IAutoLayoutService _autoLayoutService;
    private readonly IMindMapService _mindMapService;
    private readonly VisualThinkingOptions _options;
    private readonly ILogger<AiMindMapService> _logger;

    public AiMindMapService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ILLMService llmService,
        IAutoLayoutService autoLayoutService,
        IMindMapService mindMapService,
        IOptions<VisualThinkingOptions> options,
        ILogger<AiMindMapService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _llmService = llmService;
        _autoLayoutService = autoLayoutService;
        _mindMapService = mindMapService;
        _options = options.Value;
        _logger = logger;
    }

    private async Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return false;

        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted &&
                           (w.OwnerId == userId.Value || w.Members.Any(m => m.UserId == userId.Value && !m.IsDeleted)),
                      cancellationToken);
    }

    public async Task<Result<GeneratedMindMapDto>> GenerateMindMapAsync(Guid workspaceId, GenerateMindMapRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<GeneratedMindMapDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<GeneratedMindMapDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Result.Failure<GeneratedMindMapDto>(new Error("AiMindMap.Validation", "Prompt is required."));
        }

        var maxNodes = Math.Clamp(request.MaxNodes, 2, _options.MaxNodesPerMindMap);

        // Resolve source content if specified
        string sourceTitle = request.Prompt.Trim();
        string sourceContent = request.Prompt.Trim();
        Guid? sourceDocId = null;
        Guid? sourcePageId = null;
        Guid? sourceNoteId = null;
        Guid? sourceTopicId = null;

        if (!string.IsNullOrWhiteSpace(request.SourceType) && request.SourceId.HasValue)
        {
            var sourceRes = await ResolveSourceContentAsync(workspaceId, request.SourceType, request.SourceId.Value, cancellationToken);
            if (!sourceRes.IsSuccess)
            {
                return Result.Failure<GeneratedMindMapDto>(sourceRes.Error);
            }

            sourceTitle = sourceRes.Value.Title;
            sourceContent = sourceRes.Value.Content;
            sourceDocId = sourceRes.Value.DocId;
            sourcePageId = sourceRes.Value.PageId;
            sourceNoteId = sourceRes.Value.NoteId;
            sourceTopicId = sourceRes.Value.TopicId;
        }

        var systemPrompt = "You are an expert knowledge architect. Create a structured, hierarchical Mind Map graph based on the provided material.\n" +
                           "Output ONLY a valid JSON object matching this schema exactly:\n" +
                           "{\n" +
                           "  \"title\": \"Mind Map Title\",\n" +
                           "  \"description\": \"Overview of the concept hierarchy\",\n" +
                           "  \"nodes\": [\n" +
                           "    {\n" +
                           "      \"temporaryId\": \"root\",\n" +
                           "      \"title\": \"Central Concept\",\n" +
                           "      \"description\": \"Short summary\",\n" +
                           "      \"nodeType\": \"Concept\"\n" +
                           "    },\n" +
                           "    {\n" +
                           "      \"temporaryId\": \"node-1\",\n" +
                           "      \"title\": \"Sub Topic\",\n" +
                           "      \"description\": \"Short details\",\n" +
                           "      \"nodeType\": \"Concept\"\n" +
                           "    }\n" +
                           "  ],\n" +
                           "  \"edges\": [\n" +
                           "    {\n" +
                           "      \"source\": \"root\",\n" +
                           "      \"target\": \"node-1\",\n" +
                           "      \"label\": \"includes\",\n" +
                           "      \"edgeType\": \"RelatesTo\"\n" +
                           "    }\n" +
                           "  ]\n" +
                           "}\n" +
                           "Rules:\n" +
                           "- temporaryId must be unique strings (e.g. 'root', 'node-1', 'node-2').\n" +
                           "- Edge source and target must match temporaryId of defined nodes.\n" +
                           "- Do not create self-edges (source == target is forbidden).\n" +
                           $"- Provide between 2 and {maxNodes} nodes.\n" +
                           "- Do not wrap the JSON in Markdown code fences if possible, or use standard ```json.";

        var userPromptBuilder = new StringBuilder();
        userPromptBuilder.AppendLine($"Topic/Prompt: {request.Prompt}");
        if (!string.IsNullOrWhiteSpace(sourceTitle) && sourceTitle != request.Prompt)
        {
            userPromptBuilder.AppendLine($"Source: {sourceTitle}");
        }
        userPromptBuilder.AppendLine($"Maximum Nodes: {maxNodes}");
        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine("Reference Content:");
        userPromptBuilder.AppendLine(sourceContent);

        var llmRequest = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", userPromptBuilder.ToString()) },
            SystemPrompt: systemPrompt,
            Temperature: 0.2
        );

        LLMResponse llmResponse;
        try
        {
            llmResponse = await _llmService.ChatAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call LLM for mind map generation in workspace {WorkspaceId}", workspaceId);
            return Result.Failure<GeneratedMindMapDto>(new Error("LLM.InvocationError", "Failed to communicate with the AI language service."));
        }

        // Parse and validate structured output
        var parseResult = ParseAndValidateAiOutput(llmResponse.Content, maxNodes);
        if (!parseResult.IsSuccess)
        {
            _logger.LogWarning("AI mind map generation failed validation: {Error}", parseResult.Error.Description);
            return Result.Failure<GeneratedMindMapDto>(parseResult.Error);
        }

        var parsed = parseResult.Value;

        // Persist validated graph atomically
        var mindMap = new MindMap(Guid.NewGuid())
        {
            WorkspaceId = workspaceId,
            UserId = request.IsPersonal ? userId.Value : null,
            Title = parsed.Title.Length > _options.MaxTitleLength ? parsed.Title[.._options.MaxTitleLength] : parsed.Title,
            Description = parsed.Description
        };

        var tempIdToGuid = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in parsed.Nodes)
        {
            tempIdToGuid[node.TemporaryId] = Guid.NewGuid();
        }

        var initialNodes = parsed.Nodes.Select(pn => new MindMapNodeDto(
            tempIdToGuid[pn.TemporaryId],
            mindMap.Id,
            null,
            pn.Title,
            pn.Description,
            0,
            0,
            180,
            80,
            "#3B82F6",
            "RoundedRectangle",
            pn.NodeType,
            null,
            null,
            DateTime.UtcNow,
            null
        )).ToList();

        var initialEdges = parsed.Edges.Select(pe => new MindMapEdgeDto(
            Guid.NewGuid(),
            mindMap.Id,
            tempIdToGuid[pe.Source],
            tempIdToGuid[pe.Target],
            pe.Label,
            null,
            "Solid",
            pe.EdgeType,
            DateTime.UtcNow,
            null
        )).ToList();

        // Calculate layout positions so the generated mind map is neatly organized
        var layoutRes = await _autoLayoutService.ApplyLayoutAsync(
            initialNodes,
            initialEdges,
            new ApplyLayoutRequest(AutoLayoutAlgorithm.Tree),
            cancellationToken);

        var positions = layoutRes.IsSuccess
            ? layoutRes.Value.Positions.ToDictionary(p => p.NodeId)
            : new Dictionary<Guid, NodePositionDto>();

        var dbNodes = new List<MindMapNode>();
        foreach (var pn in parsed.Nodes)
        {
            var id = tempIdToGuid[pn.TemporaryId];
            var posX = positions.TryGetValue(id, out var pos) ? pos.X : 0.0;
            var posY = positions.TryGetValue(id, out var pos2) ? pos2.Y : 0.0;

            dbNodes.Add(new MindMapNode(id)
            {
                MindMapId = mindMap.Id,
                Title = pn.Title.Length > _options.MaxTitleLength ? pn.Title[.._options.MaxTitleLength] : pn.Title,
                Description = pn.Description,
                PositionX = posX,
                PositionY = posY,
                Width = 180,
                Height = 80,
                ColorHex = "#3B82F6",
                Shape = "RoundedRectangle",
                NodeType = pn.NodeType
            });
        }

        // Set root node
        if (dbNodes.Count > 0)
        {
            mindMap.RootNodeId = dbNodes[0].Id;
        }

        var dbEdges = new List<MindMapEdge>();
        foreach (var pe in parsed.Edges)
        {
            dbEdges.Add(new MindMapEdge(Guid.NewGuid())
            {
                MindMapId = mindMap.Id,
                SourceNodeId = tempIdToGuid[pe.Source],
                TargetNodeId = tempIdToGuid[pe.Target],
                Label = pe.Label != null && pe.Label.Length > _options.MaxLabelLength ? pe.Label[.._options.MaxLabelLength] : pe.Label,
                Style = "Solid",
                EdgeType = pe.EdgeType
            });
        }

        // Provenance tracking
        var aiGeneration = new AiGeneration(Guid.NewGuid())
        {
            WorkspaceId = workspaceId,
            UserId = userId.Value,
            Operation = "MindMapGeneration",
            Content = request.Prompt,
            StructuredContentJson = llmResponse.Content,
            Model = llmResponse.Model ?? "default-model",
            SourceDocumentId = sourceDocId,
            SourcePageId = sourcePageId,
            SourceNoteId = sourceNoteId
        };

        if (sourceDocId.HasValue || sourcePageId.HasValue || sourceNoteId.HasValue || sourceTopicId.HasValue)
        {
            aiGeneration.Sources.Add(new AiGenerationSource(Guid.NewGuid())
            {
                AiGenerationId = aiGeneration.Id,
                DocumentId = sourceDocId,
                PageId = sourcePageId,
                NoteId = sourceNoteId,
                Title = sourceTitle,
                RelevanceScore = 1.0
            });
        }

        _context.MindMaps.Add(mindMap);
        _context.MindMapNodes.AddRange(dbNodes);
        _context.MindMapEdges.AddRange(dbEdges);
        _context.AiGenerations.Add(aiGeneration);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully generated mind map {MindMapId} with {NodeCount} nodes and {EdgeCount} edges for workspace {WorkspaceId}",
            mindMap.Id, dbNodes.Count, dbEdges.Count, workspaceId);

        return Result.Success(new GeneratedMindMapDto(
            mindMap.Id,
            mindMap.Title,
            dbNodes.Count,
            dbEdges.Count,
            aiGeneration.Id
        ));
    }

    public async Task<Result<NodeExplanationDto>> ExplainNodeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, ExplainNodeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<NodeExplanationDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<NodeExplanationDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var node = await _context.MindMapNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

        if (node == null)
        {
            return Result.Failure<NodeExplanationDto>(new Error("MindMapNode.NotFound", "Node not found on this mind map."));
        }

        // Bounded knowledge context
        string knowledgeSummary = string.Empty;
        var sourceRefs = new List<string>();

        if (!string.IsNullOrWhiteSpace(node.LinkedEntityType) && node.LinkedEntityId.HasValue)
        {
            var ctxRes = await _mindMapService.GetNodeKnowledgeContextAsync(workspaceId, mindMapId, nodeId, cancellationToken);
            if (ctxRes.IsSuccess)
            {
                knowledgeSummary = $"Linked {ctxRes.Value.EntityType}: {ctxRes.Value.EntityTitle}\n{ctxRes.Value.Snippet}";
                sourceRefs.Add($"{ctxRes.Value.EntityType}: {ctxRes.Value.EntityTitle}");
            }
        }

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Concept: {node.Title}");
        if (!string.IsNullOrWhiteSpace(node.Description))
        {
            promptBuilder.AppendLine($"Description: {node.Description}");
        }
        if (!string.IsNullOrWhiteSpace(knowledgeSummary))
        {
            promptBuilder.AppendLine($"Knowledge Context:\n{knowledgeSummary}");
        }
        if (!string.IsNullOrWhiteSpace(request?.CustomQuestion))
        {
            promptBuilder.AppendLine($"User Question: {request.CustomQuestion}");
        }

        var systemPrompt = "You are a senior domain expert explaining a concept from a visual knowledge mind map. " +
                           "Provide a clear, grounded 2-3 paragraph explanation followed by 3-5 bulleted key takeaways. " +
                           "Format key takeaways with a leading bullet point '-' on separate lines.";

        var llmRequest = new LLMRequest(
            Messages: new[] { new LLMChatMessage("user", promptBuilder.ToString()) },
            SystemPrompt: systemPrompt,
            Temperature: 0.3
        );

        LLMResponse response;
        try
        {
            response = await _llmService.ChatAsync(llmRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call LLM for node explanation {NodeId}", nodeId);
            return Result.Failure<NodeExplanationDto>(new Error("LLM.InvocationError", "Failed to get explanation from AI service."));
        }

        var lines = response.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var takeaways = lines
            .Where(l => l.StartsWith("-") || l.StartsWith("*") || (l.Length > 2 && char.IsDigit(l[0]) && l[1] == '.'))
            .Select(l => l.TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' '))
            .Take(5)
            .ToList();

        if (takeaways.Count == 0)
        {
            takeaways.Add($"Core concept: {node.Title}");
            takeaways.Add("Review linked knowledge items for further background.");
        }

        return Result.Success(new NodeExplanationDto(
            node.Id,
            node.Title,
            response.Content,
            takeaways,
            sourceRefs
        ));
    }

    public async Task<Result<RelatedKnowledgeResultDto>> FindRelatedKnowledgeAsync(Guid workspaceId, Guid mindMapId, Guid nodeId, FindRelatedKnowledgeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<RelatedKnowledgeResultDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<RelatedKnowledgeResultDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var node = await _context.MindMapNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.MindMapId == mindMapId && !n.IsDeleted, cancellationToken);

        if (node == null)
        {
            return Result.Failure<RelatedKnowledgeResultDto>(new Error("MindMapNode.NotFound", "Node not found on this mind map."));
        }

        var limit = Math.Clamp(request.Limit, 1, 20);
        var searchTerms = $"{node.Title} {node.Description}".Trim();
        var keywords = searchTerms.Split(new[] { ' ', ',', '.', ';', ':', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .Take(5)
            .ToList();

        var relatedItems = new List<RelatedKnowledgeItemDto>();

        // Query Pages
        var pages = await _context.Pages
            .AsNoTracking()
            .Where(p => p.WorkspaceId == workspaceId && !p.IsDeleted)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var page in pages)
        {
            var matchCount = keywords.Count(k => page.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                                 (page.ContentJson != null && page.ContentJson.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (matchCount > 0)
            {
                var snippet = page.ContentJson?.Length > 150 ? page.ContentJson[..150] + "..." : page.ContentJson ?? string.Empty;
                relatedItems.Add(new RelatedKnowledgeItemDto(page.Id, page.Title, "Page", snippet, Math.Min(1.0, 0.4 + (matchCount * 0.2))));
            }
        }

        // Query Notes
        var notes = await _context.Notes
            .AsNoTracking()
            .Where(n => n.WorkspaceId == workspaceId && !n.IsDeleted)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var note in notes)
        {
            var matchCount = keywords.Count(k => note.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                                 (note.Content != null && note.Content.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (matchCount > 0)
            {
                var snippet = note.Content?.Length > 150 ? note.Content[..150] + "..." : note.Content ?? string.Empty;
                relatedItems.Add(new RelatedKnowledgeItemDto(note.Id, note.Title, "Note", snippet, Math.Min(1.0, 0.4 + (matchCount * 0.2))));
            }
        }

        // Query Documents
        var docs = await _context.Documents
            .AsNoTracking()
            .Where(d => d.WorkspaceId == workspaceId && !d.IsDeleted)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var doc in docs)
        {
            var matchCount = keywords.Count(k => doc.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                                 (doc.Summary != null && doc.Summary.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                                                 (doc.ExtractedText != null && doc.ExtractedText.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (matchCount > 0)
            {
                var snippet = doc.Summary ?? (doc.ExtractedText?.Length > 150 ? doc.ExtractedText[..150] + "..." : doc.ExtractedText ?? string.Empty);
                relatedItems.Add(new RelatedKnowledgeItemDto(doc.Id, doc.Title, "Document", snippet, Math.Min(1.0, 0.5 + (matchCount * 0.2))));
            }
        }

        // Query Study Topics
        var topics = await _context.StudyTopics
            .AsNoTracking()
            .Where(t => t.WorkspaceId == workspaceId && !t.IsDeleted)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var topic in topics)
        {
            var matchCount = keywords.Count(k => topic.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                                 (topic.Description != null && topic.Description.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (matchCount > 0)
            {
                relatedItems.Add(new RelatedKnowledgeItemDto(topic.Id, topic.Title, "StudyTopic", topic.Description ?? string.Empty, Math.Min(1.0, 0.5 + (matchCount * 0.2))));
            }
        }

        var topResults = relatedItems
            .OrderByDescending(r => r.RelevanceScore)
            .Take(limit)
            .ToList();

        return Result.Success(new RelatedKnowledgeResultDto(node.Id, topResults));
    }

    private async Task<Result<(string Title, string Content, Guid? DocId, Guid? PageId, Guid? NoteId, Guid? TopicId)>>
        ResolveSourceContentAsync(Guid workspaceId, string sourceType, Guid sourceId, CancellationToken ct)
    {
        var maxChars = 8000;

        if (sourceType.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            var doc = await _context.Documents
                .AsNoTracking()
                .Where(d => d.Id == sourceId && d.WorkspaceId == workspaceId && !d.IsDeleted)
                .Select(d => new { d.Id, d.Title, d.ExtractedText, d.Summary })
                .FirstOrDefaultAsync(ct);

            if (doc == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Document not found."));
            }

            string content = doc.Summary ?? doc.ExtractedText ?? string.Empty;
            if (content.Length > maxChars) content = content[..maxChars];

            return Result.Success((doc.Title, content, (Guid?)doc.Id, (Guid?)null, (Guid?)null, (Guid?)null));
        }

        if (sourceType.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            var page = await _context.Pages
                .AsNoTracking()
                .Where(p => p.Id == sourceId && p.WorkspaceId == workspaceId && !p.IsDeleted)
                .Select(p => new { p.Id, p.Title, p.ContentJson })
                .FirstOrDefaultAsync(ct);

            if (page == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Page not found."));
            }

            string content = page.ContentJson ?? string.Empty;
            if (content.Length > maxChars) content = content[..maxChars];

            return Result.Success((page.Title, content, (Guid?)null, (Guid?)page.Id, (Guid?)null, (Guid?)null));
        }

        if (sourceType.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            var note = await _context.Notes
                .AsNoTracking()
                .Where(n => n.Id == sourceId && n.WorkspaceId == workspaceId && !n.IsDeleted)
                .Select(n => new { n.Id, n.Title, n.Content })
                .FirstOrDefaultAsync(ct);

            if (note == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Note not found."));
            }

            string content = note.Content ?? string.Empty;
            if (content.Length > maxChars) content = content[..maxChars];

            return Result.Success((note.Title, content, (Guid?)null, (Guid?)null, (Guid?)note.Id, (Guid?)null));
        }

        if (sourceType.Equals("Topic", StringComparison.OrdinalIgnoreCase))
        {
            var currentUserId = _currentUserService.UserId;
            var topic = await _context.StudyTopics
                .AsNoTracking()
                .Where(t => t.Id == sourceId && t.WorkspaceId == workspaceId && (!currentUserId.HasValue || t.UserId == currentUserId.Value) && !t.IsDeleted)
                .Select(t => new { t.Id, t.Title, t.Description })
                .FirstOrDefaultAsync(ct);

            if (topic == null)
            {
                return Result.Failure<(string, string, Guid?, Guid?, Guid?, Guid?)>(new Error("Source.NotFound", "Study topic not found."));
            }

            var content = $"{topic.Title}\n{topic.Description ?? string.Empty}".Trim();
            return Result.Success((topic.Title, content, (Guid?)null, (Guid?)null, (Guid?)null, (Guid?)topic.Id));
        }

        return Result.Failure<(string, string, Guid?, Guid?, Guid?, Guid?)>(
            new Error("Source.InvalidType", $"Unsupported source type '{sourceType}'. Supported: Document, Page, Note, Topic."));
    }

    private static Result<ParsedAiMindMap> ParseAndValidateAiOutput(string rawJson, int maxNodes)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.EmptyResponse", "AI service returned an empty response."));
        }

        var json = rawJson.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            json = json[7..];
        }
        else if (json.StartsWith("```"))
        {
            json = json[3..];
        }
        if (json.EndsWith("```"))
        {
            json = json[..^3];
        }
        json = json.Trim();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.InvalidJson", "AI response did not contain valid JSON."));
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.InvalidStructure", "Root JSON must be an object."));
            }

            var title = root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                ? titleProp.GetString()?.Trim() ?? "Generated Mind Map"
                : "Generated Mind Map";

            var description = root.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                ? descProp.GetString()?.Trim()
                : null;

            if (!root.TryGetProperty("nodes", out var nodesProp) || nodesProp.ValueKind != JsonValueKind.Array)
            {
                return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.MissingNodes", "JSON must contain a 'nodes' array."));
            }

            var parsedNodes = new List<ParsedAiNode>();
            var seenTempIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var nodeEl in nodesProp.EnumerateArray())
            {
                if (nodeEl.ValueKind != JsonValueKind.Object) continue;

                var tempId = nodeEl.TryGetProperty("temporaryId", out var idProp) ? idProp.GetString()?.Trim() : null;
                var nodeTitle = nodeEl.TryGetProperty("title", out var nTitleProp) ? nTitleProp.GetString()?.Trim() : null;
                var nodeDesc = nodeEl.TryGetProperty("description", out var nDescProp) ? nDescProp.GetString()?.Trim() : null;
                var nodeTypeStr = nodeEl.TryGetProperty("nodeType", out var typeProp) ? typeProp.GetString()?.Trim() : null;

                if (string.IsNullOrWhiteSpace(tempId) || string.IsNullOrWhiteSpace(nodeTitle))
                {
                    return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.InvalidNode", "Each node must have a non-empty temporaryId and title."));
                }

                if (!seenTempIds.Add(tempId))
                {
                    return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.DuplicateTempId", $"Duplicate temporaryId '{tempId}' found in generated nodes."));
                }

                var nodeType = MindMapNodeType.Concept;
                if (!string.IsNullOrWhiteSpace(nodeTypeStr) && Enum.TryParse<MindMapNodeType>(nodeTypeStr, true, out var parsedType))
                {
                    nodeType = parsedType;
                }

                parsedNodes.Add(new ParsedAiNode(tempId, nodeTitle, nodeDesc, nodeType));
            }

            if (parsedNodes.Count < 1)
            {
                return Result.Failure<ParsedAiMindMap>(new Error("AiMindMap.NoNodes", "At least one node must be generated."));
            }

            if (parsedNodes.Count > maxNodes)
            {
                parsedNodes = parsedNodes.Take(maxNodes).ToList();
            }

            var validTempIds = parsedNodes.Select(n => n.TemporaryId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var parsedEdges = new List<ParsedAiEdge>();
            var seenEdges = new HashSet<(string, string)>();

            if (root.TryGetProperty("edges", out var edgesProp) && edgesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var edgeEl in edgesProp.EnumerateArray())
                {
                    if (edgeEl.ValueKind != JsonValueKind.Object) continue;

                    var source = edgeEl.TryGetProperty("source", out var srcProp) ? srcProp.GetString()?.Trim() : null;
                    var target = edgeEl.TryGetProperty("target", out var tgtProp) ? tgtProp.GetString()?.Trim() : null;
                    var label = edgeEl.TryGetProperty("label", out var lblProp) ? lblProp.GetString()?.Trim() : null;
                    var edgeTypeStr = edgeEl.TryGetProperty("edgeType", out var eTypeProp) ? eTypeProp.GetString()?.Trim() : null;

                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                    {
                        continue;
                    }

                    // Reject self-edges
                    if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Reject edges pointing to non-existent temporary IDs
                    if (!validTempIds.Contains(source) || !validTempIds.Contains(target))
                    {
                        continue;
                    }

                    var key = (source.ToLowerInvariant(), target.ToLowerInvariant());
                    if (!seenEdges.Add(key))
                    {
                        continue; // skip duplicate edge
                    }

                    var edgeType = MindMapEdgeType.RelatesTo;
                    if (!string.IsNullOrWhiteSpace(edgeTypeStr) && Enum.TryParse<MindMapEdgeType>(edgeTypeStr, true, out var parsedEType))
                    {
                        edgeType = parsedEType;
                    }

                    parsedEdges.Add(new ParsedAiEdge(source, target, label, edgeType));
                }
            }

            return Result.Success(new ParsedAiMindMap(title, description, parsedNodes, parsedEdges));
        }
    }

    private sealed record ParsedAiMindMap(
        string Title,
        string? Description,
        List<ParsedAiNode> Nodes,
        List<ParsedAiEdge> Edges
    );

    private sealed record ParsedAiNode(
        string TemporaryId,
        string Title,
        string? Description,
        MindMapNodeType NodeType
    );

    private sealed record ParsedAiEdge(
        string Source,
        string Target,
        string? Label,
        MindMapEdgeType EdgeType
    );
}
