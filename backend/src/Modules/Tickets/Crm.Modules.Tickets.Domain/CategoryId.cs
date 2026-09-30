using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

public sealed record CategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static CategoryId New() => new(Guid.NewGuid());
}
