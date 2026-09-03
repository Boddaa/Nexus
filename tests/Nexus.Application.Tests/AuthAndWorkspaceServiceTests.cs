using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.DTOs.Auth;
using Nexus.Application.DTOs.Workspaces;
using Nexus.Application.Features.Auth.Services;
using Nexus.Application.Features.Workspaces.Services;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Security;
using Xunit;

namespace Nexus.Application.Tests;

public class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; } = "User";
    public bool IsAuthenticated => UserId.HasValue;
}

public class AuthServiceTests
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly TestCurrentUserService _currentUserService;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);
        _passwordHasher = new BcryptPasswordHasher();
        _tokenService = new JwtTokenService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        _authService = new AuthService(_context, _passwordHasher, _tokenService, _currentUserService);
    }

    [Fact]
    public async Task RegisterAsync_Should_Create_User_And_Default_Workspace()
    {
        // Arrange
        var request = new RegisterRequest("architect@nexus.ai", "SecurePassword123!", "Senior Architect");

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("architect@nexus.ai", result.Value.Email);
        Assert.Equal("Senior Architect", result.Value.FullName);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Token));

        var user = await _context.Users.Include(u => u.OwnedWorkspaces).FirstOrDefaultAsync(u => u.Email == "architect@nexus.ai");
        Assert.NotNull(user);
        Assert.Single(user.OwnedWorkspaces);
        Assert.Equal("My Knowledge Base", user.OwnedWorkspaces.First().Name);
    }

    [Fact]
    public async Task RegisterAsync_Duplicate_Email_Should_Fail()
    {
        // Arrange
        var request = new RegisterRequest("duplicate@nexus.ai", "Password123!", "User One");
        await _authService.RegisterAsync(request);

        // Act
        var duplicateResult = await _authService.RegisterAsync(request);

        // Assert
        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal("Auth.DuplicateEmail", duplicateResult.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_Valid_Credentials_Should_Succeed()
    {
        // Arrange
        var registerRequest = new RegisterRequest("login@nexus.ai", "MyPassword123!", "Login User");
        await _authService.RegisterAsync(registerRequest);

        // Act
        var loginResult = await _authService.LoginAsync(new LoginRequest("login@nexus.ai", "MyPassword123!"));

        // Assert
        Assert.True(loginResult.IsSuccess);
        Assert.Equal("login@nexus.ai", loginResult.Value.Email);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Value.Token));
    }

    [Fact]
    public async Task LoginAsync_Invalid_Password_Should_Fail()
    {
        // Arrange
        var registerRequest = new RegisterRequest("invalid@nexus.ai", "CorrectPassword123!", "Test User");
        await _authService.RegisterAsync(registerRequest);

        // Act
        var loginResult = await _authService.LoginAsync(new LoginRequest("invalid@nexus.ai", "WrongPassword!"));

        // Assert
        Assert.False(loginResult.IsSuccess);
        Assert.Equal("Auth.InvalidCredentials", loginResult.Error.Code);
    }
}

public class WorkspaceServiceTests
{
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly WorkspaceService _workspaceService;

    public WorkspaceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _currentUserService = new TestCurrentUserService();
        _context = new AppDbContext(options, _currentUserService);
        _workspaceService = new WorkspaceService(_context, _currentUserService);
    }

    [Fact]
    public async Task CreateWorkspace_Should_Add_Workspace_For_Authenticated_User()
    {
        // Arrange
        var user = new User { Email = "owner@nexus.ai", FullName = "Owner User" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _currentUserService.UserId = user.Id;
        _currentUserService.Email = user.Email;

        var request = new CreateWorkspaceRequest("EF Core Mastery", "Deep dive into EF Core", "🧠", "#6366F1");

        // Act
        var result = await _workspaceService.CreateWorkspaceAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("EF Core Mastery", result.Value.Name);
        Assert.Equal(user.Id, result.Value.OwnerId);

        var listResult = await _workspaceService.GetUserWorkspacesAsync();
        Assert.True(listResult.IsSuccess);
        Assert.Single(listResult.Value);
    }

    [Fact]
    public async Task DeleteWorkspace_Should_Soft_Delete_Workspace()
    {
        // Arrange
        var user = new User { Email = "owner2@nexus.ai", FullName = "Owner 2" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _currentUserService.UserId = user.Id;
        _currentUserService.Email = user.Email;

        var createResult = await _workspaceService.CreateWorkspaceAsync(new CreateWorkspaceRequest("To Delete", null));
        var wsId = createResult.Value.Id;

        // Act
        var deleteResult = await _workspaceService.DeleteWorkspaceAsync(wsId);

        // Assert
        Assert.True(deleteResult.IsSuccess);

        var wsInDb = await _context.Workspaces.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == wsId);
        Assert.NotNull(wsInDb);
        Assert.True(wsInDb.IsDeleted);
    }
}
