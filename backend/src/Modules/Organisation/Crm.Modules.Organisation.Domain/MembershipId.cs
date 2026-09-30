using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

public sealed record MembershipId(Guid Value) : StronglyTypedId(Value)
{
    public static MembershipId New() => new(Guid.NewGuid());
}
