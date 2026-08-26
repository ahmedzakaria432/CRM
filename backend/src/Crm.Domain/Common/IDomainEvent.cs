namespace Crm.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
