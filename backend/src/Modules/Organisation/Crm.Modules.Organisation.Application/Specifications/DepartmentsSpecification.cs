using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class DepartmentsSpecification : Specification<Department>
{
    public DepartmentsSpecification(Guid? branchId, bool activeOnly)
    {
        // Specification.Where REPLACES Criteria rather than ANDing it - both conditions must therefore be
        // combined in a single call. "branch == null" and "!activeOnly" never touch the query parameter d, so
        // EF Core pre-evaluates them client-side; an unset filter becomes a no-op predicate, not a runtime check.
        BranchId? branch = branchId is { } id ? new BranchId(id) : null;
        Where(d => (branch == null || d.BranchId.Equals(branch)) && (!activeOnly || d.IsActive));

        // See BranchesSpecification - order by the whole converted property, not .Value.
        ApplyOrderBy(d => d.Code);
        AsReadOnly();
    }
}
