using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Application.Specifications;

/// <summary>Categories of the caller's tenant (the tenant scope filter applies), optionally narrowed to departments,
/// to active ones, and to customer-visible ones. Ordered by display order, then English name.</summary>
public sealed class CategoriesSpecification : Specification<Category>
{
    public CategoriesSpecification(IReadOnlyList<DepartmentId>? departments, bool activeOnly, bool customerVisibleOnly)
    {
        Where(c => (departments == null || departments.Contains(c.DepartmentId))
            && (!activeOnly || c.IsActive)
            && (!customerVisibleOnly || c.IsCustomerVisible));

        ApplyOrderBy(c => c.DisplayOrder);
        ApplyThenBy(c => c.Name.English);
        AsReadOnly();
    }
}
