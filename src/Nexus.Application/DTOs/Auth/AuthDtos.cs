namespace Nexus.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public record LoginRequest(
    string Email,
    string Password);

public record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Token,
    DateTime ExpiresAtUtc);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string? AvatarUrl,
    DateTime CreatedAtUtc);
