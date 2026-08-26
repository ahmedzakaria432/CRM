namespace Crm.Domain.Common;

/// <summary>
/// Base for a strongly-typed aggregate id, e.g. <c>public sealed record CustomerId(Guid Value) : StronglyTypedId(Value);</c>
/// </summary>
public abstract record StronglyTypedId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
