using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

/// <summary>Customers (in the caller's scope) with a contact whose normalised email equals the given one. At most 20.</summary>
public sealed class CustomersWithContactEmailSpecification : Specification<Customer>
{
    public CustomersWithContactEmailSpecification(string email)
    {
        Where(c => c.Contacts.Any(ct => ct.Email != null && ct.Email.Value == email));

        ApplyOrderBy(c => c.Number.Value);
        ApplyPaging(0, 20);
        AsReadOnly();
    }
}
