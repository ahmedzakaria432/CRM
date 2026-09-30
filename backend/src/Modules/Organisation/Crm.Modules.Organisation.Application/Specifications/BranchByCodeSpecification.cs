using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class BranchByCodeSpecification : Specification<Branch>
{
    public BranchByCodeSpecification(OrganisationCode code)
    {
        // .Equals, not == - EF Core translates a mapped scalar's .Equals(constant) into a SQL comparison;
        // ValueObject's == operator is a real method call (GetEqualityComponents().SequenceEqual) that is not.
        Where(b => b.Code.Equals(code));
        AsReadOnly();
    }
}
