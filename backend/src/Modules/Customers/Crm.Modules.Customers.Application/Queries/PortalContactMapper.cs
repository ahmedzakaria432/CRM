using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Queries;

/// <summary>The one mapping behind both portal sign-in lookups (Story 47): only what sign-in needs.</summary>
internal static class PortalContactMapper
{
    public static PortalContactMatch? ToMatch(Customer customer, Contact contact) =>
        contact.Email is { } email
            ? new PortalContactMatch(
                customer.Id.Value, contact.Id, contact.Name, email.Value, contact.IsPrimary, customer.PreferredLanguage.Value,
                customer.TenantId.Value, customer.BranchId.Value, customer.DepartmentId.Value)
            : null;
}
