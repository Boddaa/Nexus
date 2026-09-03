using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Auth;
using Nexus.Domain.Common;
using Nexus.Domain.Entities;

namespace Nexus.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        IAppDbContext context,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.DuplicateEmail", "A user with this email address already exists."));
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = passwordHash,
            Role = "User",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Users.Add(user);

        // Automatically create a default "Personal Knowledge" workspace for the new user
        var defaultWorkspace = new Workspace
        {
            Name = "My Knowledge Base",
            Description = "Your personal workspace for notes, documents, and AI learning.",
            Icon = "🧠",
            ColorHex = "#6366F1",
            OwnerId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Workspaces.Add(defaultWorkspace);

        await _context.SaveChangesAsync(cancellationToken);

        var token = _tokenService.GenerateJwtToken(user);
        var expiration = _tokenService.GetTokenExpiration();

        return Result.Success(new AuthResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            token,
            expiration));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        var token = _tokenService.GenerateJwtToken(user);
        var expiration = _tokenService.GetTokenExpiration();

        return Result.Success(new AuthResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            token,
            expiration));
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<UserDto>(Error.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDto>(Error.NotFound);
        }

        return Result.Success(new UserDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.AvatarUrl,
            user.CreatedAtUtc));
    }
}
