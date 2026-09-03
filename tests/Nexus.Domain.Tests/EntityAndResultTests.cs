using Nexus.Domain.Common;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Xunit;

namespace Nexus.Domain.Tests;

public record TestDomainEvent(string EventName, DateTime OccurredOnUtc) : IDomainEvent;

public class EntityAndResultTests
{
    [Fact]
    public void Entity_Should_Add_And_Clear_DomainEvents()
    {
        // Arrange
        var user = new User { Email = "test@nexus.ai", FullName = "Test User" };
        var domainEvent = new TestDomainEvent("UserRegistered", DateTime.UtcNow);

        // Act
        user.AddDomainEvent(domainEvent);

        // Assert
        Assert.Single(user.DomainEvents);
        Assert.Contains(domainEvent, user.DomainEvents);

        // Act
        user.ClearDomainEvents();

        // Assert
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void AuditableEntity_Should_Have_Default_CreatedAtUtc_And_Not_Deleted()
    {
        // Arrange & Act
        var workspace = new Workspace
        {
            Name = "EF Core Architecture",
            Icon = "🧠"
        };

        // Assert
        Assert.NotEqual(default, workspace.CreatedAtUtc);
        Assert.False(workspace.IsDeleted);
    }

    [Fact]
    public void Result_Success_Should_Return_Value_And_Success_True()
    {
        // Arrange & Act
        var result = Result.Success("Operation Succeeded");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("Operation Succeeded", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Result_Failure_Should_Return_Error_And_Throw_On_Value_Access()
    {
        // Arrange
        var customError = new Error("Custom.Error", "Something went wrong.");

        // Act
        var result = Result.Failure<string>(customError);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(customError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
