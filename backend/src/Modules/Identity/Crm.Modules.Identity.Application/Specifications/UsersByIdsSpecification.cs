using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>A second hand-written tenant filter, next to UsersSpecification: User is not ITenantScoped (see User.cs).</summary>
public sealed class UsersByIdsSpecification : Specification<User>
{
    public UsersByIdsSpecification(TenantId tenant, IReadOnlyList<UserId> ids)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(ids);

        Where(u => u.TenantId == tenant && ids.Contains(u.Id));
        AsReadOnly();
    }
}
