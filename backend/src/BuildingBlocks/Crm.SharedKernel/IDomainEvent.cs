namespace Crm.SharedKernel;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
