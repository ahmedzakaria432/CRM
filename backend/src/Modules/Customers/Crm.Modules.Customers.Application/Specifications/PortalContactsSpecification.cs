using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

/// <summary>ONE OF THE TWO CALLERS of Specification.IgnoreOrganisationScope() (Story 47, S2): portal sign-in runs before
/// a scope exists. Only ACTIVE customers with a PORTAL-ENABLED contact matching the email or the contact id; soft
/// delete stays. At most 20, read-only.</summary>
public sealed class PortalContactsSpecification : Specification<Customer>
{
    public PortalContactsSpecification(string? email, Guid? contactId)
    {
        Where(c => c.Status == CustomerStatus.Active
            && c.Contacts.Any(ct => ct.HasPortalAccess
                && ((email != null && ct.Email != null && ct.Email.Value == email) || (contactId != null && ct.Id == contactId))));

        ApplyOrderBy(c => c.Number.Value);
        ApplyPaging(0, 20);
        AsReadOnly();
        IgnoreOrganisationScope();
    }
}
