using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class BranchesByIdsSpecification : Specification<Branch>
{
    public BranchesByIdsSpecification(IReadOnlyList<BranchId> ids)
    {
        Where(b => ids.Contains(b.Id));
        AsReadOnly();
    }
}
