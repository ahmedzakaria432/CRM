using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class DepartmentsByIdsSpecification : Specification<Department>
{
    public DepartmentsByIdsSpecification(IReadOnlyList<DepartmentId> ids)
    {
        Where(d => ids.Contains(d.Id));
        AsReadOnly();
    }
}
