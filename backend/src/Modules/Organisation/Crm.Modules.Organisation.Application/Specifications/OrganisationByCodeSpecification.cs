using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class OrganisationByCodeSpecification : Specification<Domain.Organisation>
{
    public OrganisationByCodeSpecification(OrganisationCode code)
    {
        // .Equals, not == - see BranchByCodeSpecification.
        Where(o => o.Code.Equals(code));
        AsReadOnly();
    }
}
