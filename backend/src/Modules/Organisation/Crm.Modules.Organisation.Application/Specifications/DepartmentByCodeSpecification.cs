using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class DepartmentByCodeSpecification : Specification<Department>
{
    public DepartmentByCodeSpecification(OrganisationCode code)
    {
        // See BranchByCodeSpecification - .Equals, not ==.
        Where(d => d.Code.Equals(code));
        AsReadOnly();
    }
}
