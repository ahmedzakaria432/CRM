using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

/// <summary>The customer picker: active customers only, matched on names, number, email and phone.</summary>
public sealed class CustomerLookupSpecification : Specification<Customer>
{
    public CustomerLookupSpecification(string? search, int take)
    {
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        string? phoneTerm = term is null ? null : PhoneNumber.StripSeparators(term);
        if (phoneTerm is { Length: 0 })
        {
            phoneTerm = null;
        }

        Where(c =>
            c.Status == CustomerStatus.Active
            && (term == null
                || c.DisplayName.Arabic.Contains(term)
                || c.DisplayName.English.Contains(term)
                || c.Number.Value.Contains(term)
                || c.Contacts.Any(ct =>
                    (ct.Email != null && ct.Email.Value.Contains(term))
                    || (phoneTerm != null && ct.Phone != null && ct.Phone.Value.Contains(phoneTerm)))));

        ApplyOrderBy(c => c.DisplayName.English);
        ApplyThenBy(c => c.Number.Value);
        ApplyPaging(0, take);
        AsReadOnly();
    }
}
