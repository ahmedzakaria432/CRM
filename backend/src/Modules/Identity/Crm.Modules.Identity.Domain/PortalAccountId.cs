using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

public sealed record PortalAccountId(Guid Value) : StronglyTypedId(Value)
{
    public static PortalAccountId New() => new(Guid.NewGuid());
}
