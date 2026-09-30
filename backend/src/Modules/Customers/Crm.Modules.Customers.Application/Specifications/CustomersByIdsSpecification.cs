using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

public sealed class CustomersByIdsSpecification : Specification<Customer>
{
    public CustomersByIdsSpecification(IReadOnlyList<CustomerId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // The whole converted property, never .Value (the CategoriesByIdsSpecification precedent).
        Where(c => ids.Contains(c.Id));
        AsReadOnly();
    }
}
