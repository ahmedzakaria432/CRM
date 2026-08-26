namespace Crm.Domain.Common;

/// <summary>
/// Thrown only from inside an aggregate when an invariant is violated. Never thrown from a handler.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainError Error { get; }

    public DomainException(DomainError error) : base(error.Message)
    {
        Error = error;
    }
}
