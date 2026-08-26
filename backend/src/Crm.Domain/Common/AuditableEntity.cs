namespace Crm.Domain.Common;

/// <summary>
/// No public setters and no stamp methods on purpose - an audit field a handler can write is an
/// audit field that can lie. AuditingSaveChangesInterceptor writes these through the EF entry
/// (<c>entry.Property(...).CurrentValue</c>), which needs no accessor beyond the private setter.
/// </summary>
public abstract class AuditableEntity<TId> : AggregateRoot<TId>, IAuditable
    where TId : notnull
{
    public string? CreatedBy { get; private set; }

    public DateTimeOffset? CreatedAtUtc { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    protected AuditableEntity()
    {
    }

    protected AuditableEntity(TId id) : base(id)
    {
    }
}
