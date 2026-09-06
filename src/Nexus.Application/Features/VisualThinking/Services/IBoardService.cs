using Nexus.Application.Common.Models;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;

namespace Nexus.Application.Features.VisualThinking.Services;

public interface IBoardService
{
    Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Result<BoardDetailDto>> GetBoardByIdAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BoardDto>> CreateBoardAsync(Guid workspaceId, CreateBoardRequest request, CancellationToken cancellationToken = default);
    Task<Result<BoardDto>> UpdateBoardAsync(Guid workspaceId, Guid boardId, UpdateBoardRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBoardAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BoardItemDto>>> GetBoardItemsAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BoardItemDto>> GetBoardItemByIdAsync(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<BoardItemDto>> CreateBoardItemAsync(Guid workspaceId, Guid boardId, CreateBoardItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<BoardItemDto>> UpdateBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, UpdateBoardItemRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BoardItemDto>>> BatchUpdateBoardItemsAsync(Guid workspaceId, Guid boardId, BatchUpdateBoardItemsRequest request, CancellationToken cancellationToken = default);
}
