using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

public sealed record PortalSessionId(Guid Value) : StronglyTypedId(Value)
{
    public static PortalSessionId New() => new(Guid.NewGuid());
}
