namespace Crm.SharedKernel.Scoping;

/// <summary>The organisation (tenant). Also the id of the Organisation aggregate.</summary>
public sealed record TenantId(Guid Value) : StronglyTypedId(Value)
{
    public static readonly TenantId None = new(Guid.Empty);
}

public sealed record BranchId(Guid Value) : StronglyTypedId(Value)
{
    public static BranchId New() => new(Guid.NewGuid());
}

public sealed record DepartmentId(Guid Value) : StronglyTypedId(Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());
}
