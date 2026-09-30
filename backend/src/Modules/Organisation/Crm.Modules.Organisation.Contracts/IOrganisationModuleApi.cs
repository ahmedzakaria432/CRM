using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Contracts;

/// <summary>What another module may ask the Organisation module, synchronously. Story 07 reads this at sign-in
/// to put department ids and roles on the token.</summary>
public interface IOrganisationModuleApi
{
    Task<Result<UserMemberships>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>TenantId is Guid.Empty with an empty Memberships list when the user has none - success, not
/// failure: "no memberships" is a valid answer, not an error.</summary>
public sealed record UserMemberships(Guid UserId, Guid TenantId, IReadOnlyList<UserMembership> Memberships);

public sealed record UserMembership(Guid MembershipId, Guid DepartmentId, Guid BranchId, string Role);
