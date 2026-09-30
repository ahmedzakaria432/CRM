using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Application.Specifications;

/// <summary>An existing, non-removed (user, department) membership - used to refuse a duplicate AddMembership.</summary>
public sealed class ActiveMembershipSpecification : Specification<Membership>
{
    public ActiveMembershipSpecification(Guid userId, DepartmentId departmentId)
    {
        Where(m => m.UserId == userId && m.DepartmentId.Equals(departmentId));
        AsReadOnly();
    }
}
