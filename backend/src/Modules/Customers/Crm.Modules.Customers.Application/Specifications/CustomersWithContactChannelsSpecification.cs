using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

/// <summary>Customers (in the caller's scope) with a contact whose normalised email or phone is in the given lists.
/// The duplicate warning on create. At most 10.</summary>
public sealed class CustomersWithContactChannelsSpecification : Specification<Customer>
{
    public CustomersWithContactChannelsSpecification(IReadOnlyList<string> emails, IReadOnlyList<string> phones)
    {
        Where(c => c.Contacts.Any(ct =>
            (ct.Email != null && emails.Contains(ct.Email.Value))
            || (ct.Phone != null && phones.Contains(ct.Phone.Value))));

        ApplyOrderBy(c => c.Number.Value);
        ApplyPaging(0, 10);
        AsReadOnly();
    }
}
