using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: an agent who provisions an existing account sets its password. One account per contact.</summary>
public sealed class PortalAccountByContactSpecification : Specification<PortalAccount>
{
    public PortalAccountByContactSpecification(Guid contactId)
    {
        Where(a => a.ContactId == contactId);
    }
}
