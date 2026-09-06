using Nexus.Application.DTOs.AI;
using Nexus.Application.DTOs.Notes;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.AI.Services;

public interface IAiKnowledgeService
{
    Task<Result<AiOperationResultDto>> SummarizeAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> ExplainAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> ExtractKeyPointsAsync(Guid workspaceId, AiKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> GenerateQuestionsAsync(Guid workspaceId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> GenerateStudyMaterialAsync(Guid workspaceId, GenerateStudyMaterialRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoteDto>> SaveAsNoteAsync(Guid workspaceId, SaveAiOutputAsNoteRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AiGenerationSummaryDto>>> GetGenerationsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<AiOperationResultDto>> GetGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteGenerationAsync(Guid workspaceId, Guid generationId, CancellationToken cancellationToken = default);
}
