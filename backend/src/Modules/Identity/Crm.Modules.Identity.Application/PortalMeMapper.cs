using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Application;

/// <summary>The one place a PortalMeDto is built (GetPortalMe and UpdatePortalProfile). The account must be the one the
/// token names: grant, contact and tenant have to agree, otherwise it is refused as not-a-portal-caller.</summary>
internal static class PortalMeMapper
{
    public static Result<PortalMeDto> ToMe(PortalAccount? account, OrganisationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (account is null
            || scope.PortalCustomer is not { } grant
            || grant.ContactId != account.ContactId
            || grant.CustomerId != account.CustomerId
            || scope.Tenant != account.TenantId)
        {
            return Result.Failure<PortalMeDto>(IdentityErrors.PortalOnly());
        }

        return Result.Success(new PortalMeDto(
            account.Id.Value,
            account.Email.Value,
            account.DisplayName.Value,
            account.PreferredLocale.Value,
            account.CustomerId,
            account.ContactId,
            grant.IsAccountAdmin,
            CrmPermissions.ForRoles(scope.EffectiveRoles)));
    }
}
