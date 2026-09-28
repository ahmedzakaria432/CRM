namespace Crm.SharedKernel;

/// <summary>
/// Deliberately no <c>MarkDeleted</c> method - the interceptor writes these three properties
/// through the EF entry. An aggregate that wants an intention-revealing close exposes its own
/// method (<c>ticket.Close(reason)</c>); that is a different concept from a row being soft-deleted.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAtUtc { get; }

    string? DeletedBy { get; }
}
