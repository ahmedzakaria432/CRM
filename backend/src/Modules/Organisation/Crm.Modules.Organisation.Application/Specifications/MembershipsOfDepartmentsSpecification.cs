using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class MembershipsOfDepartmentsSpecification : Specification<Membership>
{
    public MembershipsOfDepartmentsSpecification(IReadOnlyList<DepartmentId> departmentIds)
    {
        ArgumentNullException.ThrowIfNull(departmentIds);

        Where(m => departmentIds.Contains(m.DepartmentId));
        AsReadOnly();
    }
}
