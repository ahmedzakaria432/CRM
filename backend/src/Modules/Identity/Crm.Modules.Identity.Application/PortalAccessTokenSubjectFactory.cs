using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application;

internal static class PortalAccessTokenSubjectFactory
{
    /// <summary>The account is admin of its customer when its contact is the primary one (D4). Null (the caller
    /// refuses) when the contact no longer belongs to the account tenant or customer - defensive.</summary>
    public static PortalAccessTokenSubject? Build(PortalAccount account, PortalContactMatch contact, PortalSessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(sessionId);

        if (contact.TenantId != account.TenantId.Value || contact.CustomerId != account.CustomerId || contact.ContactId != account.ContactId)
        {
            return null;
        }

        return new PortalAccessTokenSubject(
            account.Id.Value, account.DisplayName.Value, account.TenantId.Value, sessionId.Value, contact.CustomerId,
            contact.ContactId, contact.IsPrimary, contact.BranchId, contact.DepartmentId);
    }
}
