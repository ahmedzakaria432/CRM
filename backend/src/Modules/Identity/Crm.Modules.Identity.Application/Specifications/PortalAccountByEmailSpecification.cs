using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: portal sign-in writes the failed-attempt counter on the account it finds. Email is unique
/// globally, so there is no tenant criterion.</summary>
public sealed class PortalAccountByEmailSpecification : Specification<PortalAccount>
{
    public PortalAccountByEmailSpecification(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);

        string value = email.Value;
        Where(a => a.Email.Value == value);
    }
}
