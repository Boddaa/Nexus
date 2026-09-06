using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Options;
using Nexus.Application.DTOs.VisualThinking;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.VisualThinking.Services;

public class BoardService : IBoardService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly VisualThinkingOptions _options;
    private readonly ILogger<BoardService> _logger;

    public BoardService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IOptions<VisualThinkingOptions> options,
        ILogger<BoardService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
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

    private async Task<bool> IsWorkspaceOwnerAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return false;

        return await _context.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.Id == workspaceId && !w.IsDeleted && w.OwnerId == userId.Value, cancellationToken);
    }

    private async Task<Board?> GetValidBoardAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken, bool tracking = false)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return null;

        var isOwner = await IsWorkspaceOwnerAsync(workspaceId, cancellationToken);

        var query = tracking ? _context.Boards : _context.Boards.AsNoTracking();

        var board = await query
            .FirstOrDefaultAsync(b => b.Id == boardId && b.WorkspaceId == workspaceId && !b.IsDeleted, cancellationToken);

        if (board == null) return null;

        // If personal board, only the owner of the board or workspace owner can access
        if (board.UserId.HasValue && board.UserId.Value != userId.Value && !isOwner)
        {
            return null;
        }

        return board;
    }

    public async Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<BoardDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<BoardDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var isOwner = await IsWorkspaceOwnerAsync(workspaceId, cancellationToken);

        var boards = await _context.Boards
            .AsNoTracking()
            .Where(b => b.WorkspaceId == workspaceId && !b.IsDeleted &&
                        (b.UserId == null || b.UserId == userId.Value || isOwner))
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BoardDto(
                b.Id,
                b.WorkspaceId,
                b.UserId,
                b.Title,
                b.Description,
                b.Type,
                b.Items.Count(i => !i.IsDeleted),
                b.CreatedAtUtc,
                b.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BoardDto>>(boards);
    }

    public async Task<Result<BoardDetailDto>> GetBoardByIdAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<BoardDetailDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<BoardDetailDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken, tracking: false);
        if (board == null)
        {
            return Result.Failure<BoardDetailDto>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        var items = await _context.BoardItems
            .AsNoTracking()
            .Where(i => i.BoardId == boardId && !i.IsDeleted)
            .OrderBy(i => i.ZIndex)
            .ThenBy(i => i.OrderIndex)
            .Take(_options.MaxBoardItemsPerBoard)
            .Select(i => new BoardItemDto(
                i.Id,
                i.BoardId,
                i.BoardColumnId,
                i.Type,
                i.Title,
                i.Description,
                i.Content,
                i.X,
                i.Y,
                i.Width,
                i.Height,
                i.Rotation,
                i.ZIndex,
                i.ColorHex,
                i.LinkedEntityType,
                i.LinkedEntityId,
                i.CreatedAtUtc,
                i.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var dto = new BoardDetailDto(
            board.Id,
            board.WorkspaceId,
            board.UserId,
            board.Title,
            board.Description,
            board.Type,
            items,
            board.CreatedAtUtc,
            board.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<BoardDto>> CreateBoardAsync(Guid workspaceId, CreateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<BoardDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<BoardDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<BoardDto>(new Error("Board.Validation", "Board title is required."));
        }

        if (request.Title.Length > _options.MaxTitleLength)
        {
            return Result.Failure<BoardDto>(new Error("Board.Validation", $"Board title cannot exceed {_options.MaxTitleLength} characters."));
        }

        var boardCount = await _context.Boards
            .CountAsync(b => b.WorkspaceId == workspaceId && !b.IsDeleted, cancellationToken);

        if (boardCount >= _options.MaxBoardsPerWorkspace)
        {
            return Result.Failure<BoardDto>(new Error("Board.LimitExceeded", $"Maximum number of boards ({_options.MaxBoardsPerWorkspace}) reached for this workspace."));
        }

        var board = new Board(Guid.NewGuid())
        {
            WorkspaceId = workspaceId,
            UserId = request.IsPersonal ? userId.Value : null,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Type = request.Type
        };

        _context.Boards.Add(board);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created board {BoardId} ({Title}) in workspace {WorkspaceId}", board.Id, board.Title, workspaceId);

        var dto = new BoardDto(
            board.Id,
            board.WorkspaceId,
            board.UserId,
            board.Title,
            board.Description,
            board.Type,
            0,
            board.CreatedAtUtc,
            board.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<BoardDto>> UpdateBoardAsync(Guid workspaceId, Guid boardId, UpdateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<BoardDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<BoardDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken, tracking: true);
        if (board == null)
        {
            return Result.Failure<BoardDto>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<BoardDto>(new Error("Board.Validation", "Board title is required."));
        }

        if (request.Title.Length > _options.MaxTitleLength)
        {
            return Result.Failure<BoardDto>(new Error("Board.Validation", $"Board title cannot exceed {_options.MaxTitleLength} characters."));
        }

        board.Title = request.Title.Trim();
        board.Description = request.Description?.Trim();
        if (request.Type.HasValue)
        {
            board.Type = request.Type.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var itemCount = await _context.BoardItems
            .CountAsync(i => i.BoardId == boardId && !i.IsDeleted, cancellationToken);

        var dto = new BoardDto(
            board.Id,
            board.WorkspaceId,
            board.UserId,
            board.Title,
            board.Description,
            board.Type,
            itemCount,
            board.CreatedAtUtc,
            board.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result> DeleteBoardAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken, tracking: true);
        if (board == null)
        {
            return Result.Failure(new Error("Board.NotFound", "Board not found or access denied."));
        }

        board.IsDeleted = true;

        // Cascade soft delete to items
        var items = await _context.BoardItems
            .Where(i => i.BoardId == boardId && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.IsDeleted = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted board {BoardId} and {ItemCount} items", boardId, items.Count);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BoardItemDto>>> GetBoardItemsAsync(Guid workspaceId, Guid boardId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken);
        if (board == null)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        var items = await _context.BoardItems
            .AsNoTracking()
            .Where(i => i.BoardId == boardId && !i.IsDeleted)
            .OrderBy(i => i.ZIndex)
            .ThenBy(i => i.OrderIndex)
            .Take(_options.MaxBoardItemsPerBoard)
            .Select(i => new BoardItemDto(
                i.Id,
                i.BoardId,
                i.BoardColumnId,
                i.Type,
                i.Title,
                i.Description,
                i.Content,
                i.X,
                i.Y,
                i.Width,
                i.Height,
                i.Rotation,
                i.ZIndex,
                i.ColorHex,
                i.LinkedEntityType,
                i.LinkedEntityId,
                i.CreatedAtUtc,
                i.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BoardItemDto>>(items);
    }

    public async Task<Result<BoardItemDto>> GetBoardItemByIdAsync(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<BoardItemDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<BoardItemDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken);
        if (board == null)
        {
            return Result.Failure<BoardItemDto>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        var item = await _context.BoardItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.BoardId == boardId && !i.IsDeleted, cancellationToken);

        if (item == null)
        {
            return Result.Failure<BoardItemDto>(new Error("BoardItem.NotFound", "Board item not found on this board."));
        }

        var dto = new BoardItemDto(
            item.Id,
            item.BoardId,
            item.BoardColumnId,
            item.Type,
            item.Title,
            item.Description,
            item.Content,
            item.X,
            item.Y,
            item.Width,
            item.Height,
            item.Rotation,
            item.ZIndex,
            item.ColorHex,
            item.LinkedEntityType,
            item.LinkedEntityId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<BoardItemDto>> CreateBoardItemAsync(Guid workspaceId, Guid boardId, CreateBoardItemRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<BoardItemDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<BoardItemDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken);
        if (board == null)
        {
            return Result.Failure<BoardItemDto>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<BoardItemDto>(new Error("BoardItem.Validation", "Item title is required."));
        }

        var currentItemCount = await _context.BoardItems
            .CountAsync(i => i.BoardId == boardId && !i.IsDeleted, cancellationToken);

        if (currentItemCount >= _options.MaxBoardItemsPerBoard)
        {
            return Result.Failure<BoardItemDto>(new Error("BoardItem.LimitExceeded", $"Maximum number of board items ({_options.MaxBoardItemsPerBoard}) reached for this board."));
        }

        var item = new BoardItem(Guid.NewGuid())
        {
            BoardId = boardId,
            BoardColumnId = request.BoardColumnId,
            Type = request.Type,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Content = request.Content?.Trim(),
            X = request.X,
            Y = request.Y,
            Width = request.Width > 0 ? request.Width : 200,
            Height = request.Height > 0 ? request.Height : 150,
            Rotation = request.Rotation,
            ZIndex = request.ZIndex,
            ColorHex = request.ColorHex,
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId
        };

        _context.BoardItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created board item {ItemId} on board {BoardId}", item.Id, boardId);

        var dto = new BoardItemDto(
            item.Id,
            item.BoardId,
            item.BoardColumnId,
            item.Type,
            item.Title,
            item.Description,
            item.Content,
            item.X,
            item.Y,
            item.Width,
            item.Height,
            item.Rotation,
            item.ZIndex,
            item.ColorHex,
            item.LinkedEntityType,
            item.LinkedEntityId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result<BoardItemDto>> UpdateBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, UpdateBoardItemRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<BoardItemDto>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<BoardItemDto>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken);
        if (board == null)
        {
            return Result.Failure<BoardItemDto>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        var item = await _context.BoardItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.BoardId == boardId && !i.IsDeleted, cancellationToken);

        if (item == null)
        {
            // Check if item exists in another board
            var existsElsewhere = await _context.BoardItems
                .AsNoTracking()
                .AnyAsync(i => i.Id == itemId && !i.IsDeleted, cancellationToken);

            if (existsElsewhere)
            {
                return Result.Failure<BoardItemDto>(new Error("BoardItem.Mismatch", "Board item does not belong to the specified board."));
            }

            return Result.Failure<BoardItemDto>(new Error("BoardItem.NotFound", "Board item not found."));
        }

        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Result.Failure<BoardItemDto>(new Error("BoardItem.Validation", "Title cannot be empty."));
            }
            item.Title = request.Title.Trim();
        }

        if (request.Description != null) item.Description = request.Description.Trim();
        if (request.Content != null) item.Content = request.Content.Trim();
        if (request.Type.HasValue) item.Type = request.Type.Value;
        if (request.X.HasValue) item.X = request.X.Value;
        if (request.Y.HasValue) item.Y = request.Y.Value;
        if (request.Width.HasValue && request.Width.Value > 0) item.Width = request.Width.Value;
        if (request.Height.HasValue && request.Height.Value > 0) item.Height = request.Height.Value;
        if (request.Rotation.HasValue) item.Rotation = request.Rotation.Value;
        if (request.ZIndex.HasValue) item.ZIndex = request.ZIndex.Value;
        if (request.ColorHex != null) item.ColorHex = request.ColorHex;
        if (request.LinkedEntityType != null) item.LinkedEntityType = request.LinkedEntityType;
        if (request.LinkedEntityId.HasValue) item.LinkedEntityId = request.LinkedEntityId.Value;
        if (request.BoardColumnId.HasValue) item.BoardColumnId = request.BoardColumnId.Value;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new BoardItemDto(
            item.Id,
            item.BoardId,
            item.BoardColumnId,
            item.Type,
            item.Title,
            item.Description,
            item.Content,
            item.X,
            item.Y,
            item.Width,
            item.Height,
            item.Rotation,
            item.ZIndex,
            item.ColorHex,
            item.LinkedEntityType,
            item.LinkedEntityId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        );

        return Result.Success(dto);
    }

    public async Task<Result> DeleteBoardItemAsync(Guid workspaceId, Guid boardId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken);
        if (board == null)
        {
            return Result.Failure(new Error("Board.NotFound", "Board not found or access denied."));
        }

        var item = await _context.BoardItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.BoardId == boardId && !i.IsDeleted, cancellationToken);

        if (item == null)
        {
            var existsElsewhere = await _context.BoardItems
                .AsNoTracking()
                .AnyAsync(i => i.Id == itemId && !i.IsDeleted, cancellationToken);

            if (existsElsewhere)
            {
                return Result.Failure(new Error("BoardItem.Mismatch", "Board item does not belong to the specified board."));
            }

            return Result.Failure(new Error("BoardItem.NotFound", "Board item not found on this board."));
        }

        item.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted board item {ItemId} from board {BoardId}", itemId, boardId);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BoardItemDto>>> BatchUpdateBoardItemsAsync(Guid workspaceId, Guid boardId, BatchUpdateBoardItemsRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Auth.Unauthorized", "User is not authenticated."));
        }

        if (!await HasWorkspaceAccessAsync(workspaceId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Workspace.AccessDenied", "User does not have access to this workspace."));
        }

        var board = await GetValidBoardAsync(workspaceId, boardId, cancellationToken);
        if (board == null)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("Board.NotFound", "Board not found or access denied."));
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return Result.Success<IReadOnlyList<BoardItemDto>>(Array.Empty<BoardItemDto>());
        }

        if (request.Items.Count > _options.MaxBatchItems)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("BoardItem.BatchLimit", $"Batch update exceeds maximum allowed items ({_options.MaxBatchItems})."));
        }

        var itemIds = request.Items.Select(x => x.Id).ToList();

        var dbItems = await _context.BoardItems
            .Where(i => itemIds.Contains(i.Id) && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        // Security check: every item must belong to this board
        var foreignItems = dbItems.Where(i => i.BoardId != boardId).ToList();
        if (foreignItems.Count > 0)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("BoardItem.Mismatch", "One or more items do not belong to the specified board."));
        }

        if (dbItems.Count != itemIds.Count)
        {
            return Result.Failure<IReadOnlyList<BoardItemDto>>(new Error("BoardItem.NotFound", "One or more items in the batch update were not found on this board."));
        }

        var itemsById = dbItems.ToDictionary(i => i.Id);

        foreach (var update in request.Items)
        {
            if (itemsById.TryGetValue(update.Id, out var item))
            {
                item.X = update.X;
                item.Y = update.Y;
                if (update.Width.HasValue && update.Width.Value > 0) item.Width = update.Width.Value;
                if (update.Height.HasValue && update.Height.Value > 0) item.Height = update.Height.Value;
                if (update.Rotation.HasValue) item.Rotation = update.Rotation.Value;
                if (update.ZIndex.HasValue) item.ZIndex = update.ZIndex.Value;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var resultDtos = dbItems.Select(item => new BoardItemDto(
            item.Id,
            item.BoardId,
            item.BoardColumnId,
            item.Type,
            item.Title,
            item.Description,
            item.Content,
            item.X,
            item.Y,
            item.Width,
            item.Height,
            item.Rotation,
            item.ZIndex,
            item.ColorHex,
            item.LinkedEntityType,
            item.LinkedEntityId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        )).ToList();

        return Result.Success<IReadOnlyList<BoardItemDto>>(resultDtos);
    }
}
