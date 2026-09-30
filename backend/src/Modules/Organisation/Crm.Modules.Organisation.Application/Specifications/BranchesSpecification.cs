using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class BranchesSpecification : Specification<Branch>
{
    public BranchesSpecification(bool activeOnly)
    {
        if (activeOnly)
        {
            Where(b => b.IsActive);
        }

        // Order by the whole converted property (not .Value): EF can translate a converted scalar as a sort
        // key directly, but cannot decompose a post-conversion member access like Code.Value server-side.
        ApplyOrderBy(b => b.Code);
        AsReadOnly();
    }
}
