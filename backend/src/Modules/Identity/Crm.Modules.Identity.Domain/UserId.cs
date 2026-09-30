using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

public sealed record UserId(Guid Value) : StronglyTypedId(Value)
{
    public static UserId New() => new(Guid.NewGuid());
}
