using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Contracts;

/// <summary>What another module may ask the Organisation module, synchronously. Story 07 reads this at sign-in
/// to put department ids and roles on the token.</summary>
public interface IOrganisationModuleApi
{
    Task<Result<UserMemberships>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Story 15: a department of the caller's tenant (Department is ITenantScoped). Unknown -> failed Result
    /// (404 Organisation.DepartmentNotFound).</summary>
    Task<Result<DepartmentReference>> GetDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default);

    /// <summary>Story 15: does this user hold a (non-removed) membership in this department of the caller's tenant.</summary>
    Task<Result<bool>> IsDepartmentMemberAsync(Guid userId, Guid departmentId, CancellationToken cancellationToken = default);

    /// <summary>Story 20: the non-removed memberships of these departments (caller's tenant). At most 100 department ids.</summary>
    Task<Result<IReadOnlyList<DepartmentMember>>> GetDepartmentMembersAsync(
        IReadOnlyCollection<Guid> departmentIds, CancellationToken cancellationToken = default);

    /// <summary>Story 20: every department of the caller's tenant (transfer targets), ordered by code.</summary>
    Task<Result<IReadOnlyList<DepartmentReference>>> ListDepartmentsAsync(bool activeOnly, CancellationToken cancellationToken = default);
}

/// <summary>TenantId is Guid.Empty with an empty Memberships list when the user has none - success, not
/// failure: "no memberships" is a valid answer, not an error.</summary>
public sealed record UserMemberships(Guid UserId, Guid TenantId, IReadOnlyList<UserMembership> Memberships);

public sealed record UserMembership(Guid MembershipId, Guid DepartmentId, Guid BranchId, string Role);

public sealed record DepartmentReference(Guid Id, Guid TenantId, Guid BranchId, string Code, string NameAr, string NameEn, bool IsActive);

public sealed record DepartmentMember(Guid UserId, Guid DepartmentId, string Role);
