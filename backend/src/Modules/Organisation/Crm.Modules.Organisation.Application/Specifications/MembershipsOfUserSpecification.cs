using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Specifications;

/// <summary>ONE OF THE TWO CALLERS of Specification.IgnoreOrganisationScope() (Story 06; the other is Customers' PortalContactsSpecification, Story 47): the caller is not signed in
/// yet, so there is no scope to filter by. The soft-delete filter stays - a removed membership never grants
/// access, even here.</summary>
public sealed class MembershipsOfUserSpecification : Specification<Membership>
{
    public MembershipsOfUserSpecification(Guid userId)
    {
        Where(m => m.UserId == userId);
        AsReadOnly();
        IgnoreOrganisationScope();
    }
}
