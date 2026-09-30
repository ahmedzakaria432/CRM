using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

public sealed record RefreshTokenFamilyId(Guid Value) : StronglyTypedId(Value)
{
    public static RefreshTokenFamilyId New() => new(Guid.NewGuid());
}
