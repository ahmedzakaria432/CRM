using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

public sealed record BrandLogoId(Guid Value) : StronglyTypedId(Value)
{
    public static BrandLogoId New() => new(Guid.NewGuid());
}
