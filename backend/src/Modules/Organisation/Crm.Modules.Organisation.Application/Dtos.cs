using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application;

// Primitives only - no SharedKernel id types cross into a DTO (keeps Swagger/pnpm gen:api shapes plain).
public sealed record LocalizedTextDto(string Ar, string En)
{
    public static LocalizedTextDto FromDomain(LocalizedText name) => new(name.Arabic, name.English);
}

public sealed record BranchDto(Guid Id, string Code, LocalizedTextDto Name, bool IsActive, byte[] RowVersion)
{
    public static BranchDto FromDomain(Branch branch) => new(
        branch.Id.Value, branch.Code.Value, LocalizedTextDto.FromDomain(branch.Name), branch.IsActive, branch.RowVersion);
}

public sealed record DepartmentDto(Guid Id, Guid BranchId, string Code, LocalizedTextDto Name, bool IsActive, byte[] RowVersion)
{
    public static DepartmentDto FromDomain(Department department) => new(
        department.Id.Value, department.BranchId.Value, department.Code.Value,
        LocalizedTextDto.FromDomain(department.Name), department.IsActive, department.RowVersion);
}

public sealed record MembershipDto(Guid Id, Guid UserId, Guid DepartmentId, Guid BranchId, string Role, byte[] RowVersion)
{
    public static MembershipDto FromDomain(Membership membership) => new(
        membership.Id.Value, membership.UserId, membership.DepartmentId.Value, membership.BranchId.Value,
        membership.Role.ToString(), membership.RowVersion);
}

public sealed record MyDepartmentDto(
    Guid DepartmentId,
    string DepartmentCode,
    LocalizedTextDto DepartmentName,
    Guid BranchId,
    LocalizedTextDto BranchName,
    string Role,
    bool IsActive);

public sealed record MyScopeDto(
    Guid UserId,
    Guid TenantId,
    Guid? ActiveDepartmentId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<MyDepartmentDto> Departments);
