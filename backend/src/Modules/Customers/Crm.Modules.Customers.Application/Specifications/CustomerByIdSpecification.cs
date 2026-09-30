using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

public sealed class CustomerByIdSpecification : Specification<Customer>
{
    public CustomerByIdSpecification(CustomerId id)
    {
        Where(c => c.Id == id);
        AsReadOnly();
    }
}
