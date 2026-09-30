using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Tickets.Application;

/// <summary>The portal customer behind a portal request (Story 47). Null for anyone else - the handler answers
/// 401 Authorization.PortalOnly. The actor of every portal action is the contact of the grant (D8).</summary>
internal static class PortalCaller
{
    public static PortalCustomerGrant? Grant(ICurrentUserService user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Scope.PortalCustomer;
    }
}
