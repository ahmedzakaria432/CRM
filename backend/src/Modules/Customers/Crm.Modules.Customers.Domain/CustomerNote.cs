using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>Internal note on a customer. Child entity - reachable only through Customer. Not IAuditable: its own
/// CreatedAtUtc is domain data, and the auditing interceptor never touches it.</summary>
public sealed class CustomerNote : Entity<Guid>
{
    public const int BodyMaxLength = 4000;

    public string Body { get; private set; }

    public bool IsPinned { get; private set; }

    public Guid AuthorId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PinnedAtUtc { get; private set; }

    private CustomerNote()
    {
        Body = string.Empty;
    }

    internal CustomerNote(Guid id, string body, Guid authorId, DateTimeOffset createdAtUtc) : base(id)
    {
        ArgumentNullException.ThrowIfNull(body);

        string trimmed = body.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(CustomerDomainErrors.NoteBodyRequired);
        }

        if (trimmed.Length > BodyMaxLength)
        {
            throw new DomainException(CustomerDomainErrors.NoteBodyTooLong);
        }

        if (authorId == Guid.Empty)
        {
            throw new DomainException(CustomerDomainErrors.NoteAuthorRequired);
        }

        Body = trimmed;
        AuthorId = authorId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Idempotent: pinning a pinned note keeps its original PinnedAtUtc.</summary>
    internal void Pin(DateTimeOffset pinnedAtUtc)
    {
        if (IsPinned)
        {
            return;
        }

        IsPinned = true;
        PinnedAtUtc = pinnedAtUtc;
    }

    internal void Unpin()
    {
        IsPinned = false;
        PinnedAtUtc = null;
    }
}
