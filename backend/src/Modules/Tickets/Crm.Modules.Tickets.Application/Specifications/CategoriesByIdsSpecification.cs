using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Specifications;

public sealed class CategoriesByIdsSpecification : Specification<Category>
{
    public CategoriesByIdsSpecification(IReadOnlyList<CategoryId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        Where(c => ids.Contains(c.Id));
        AsReadOnly();
    }
}
