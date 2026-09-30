using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Crm.BuildingBlocks.Presentation.Authorization;

/// <summary>Every Agent/Admin endpoint requires a signed-in staff user, with no attribute to forget;
/// [AllowAnonymous] on an action (sign-in, refresh, sign-out) wins. Portal: PortalAreaAuthorizationConvention (Story 47); Public is untouched
/// (story 55).</summary>
public sealed class StaffAreaAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        if (!controller.RouteValues.TryGetValue("area", out string? area)
            || (!string.Equals(area, CrmAreas.Agent, StringComparison.Ordinal)
                && !string.Equals(area, CrmAreas.Admin, StringComparison.Ordinal)))
        {
            return;
        }

        foreach (ActionModel action in controller.Actions)
        {
            foreach (SelectorModel selector in action.Selectors)
            {
                selector.EndpointMetadata.Add(new AuthorizeAttribute(CrmPolicies.Staff));
            }
        }
    }
}
