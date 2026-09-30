using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Application;

/// <summary>DEVELOPMENT ONLY (Story 06): lets the BuildingBlocks dev-login handler build a principal from this
/// module's memberships without referencing the module. Deleted by Story 07.</summary>
public sealed class DevUserDirectory(IOrganisationModuleApi organisationModuleApi) : IDevUserDirectory
{
    public async Task<DevUser?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        Result<UserMemberships> result = await organisationModuleApi.GetMembershipsAsync(userId, cancellationToken);

        if (result.IsFailure || result.Value.Memberships.Count == 0)
        {
            return null;
        }

        UserMemberships memberships = result.Value;

        return new DevUser(
            memberships.UserId,
            memberships.TenantId,
            [.. memberships.Memberships.Select(m => new DevUserMembership(m.DepartmentId, m.BranchId, m.Role))]);
    }
}
