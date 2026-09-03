namespace Nexus.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
