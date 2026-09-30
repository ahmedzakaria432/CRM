using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Crm.BuildingBlocks.Presentation.Authorization;

/// <summary>Every Portal endpoint requires a signed-in portal customer; [AllowAnonymous] on an action (register, login,
/// refresh, logout) wins (Story 47).</summary>
public sealed class PortalAreaAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        if (!controller.RouteValues.TryGetValue("area", out string? area)
            || !string.Equals(area, CrmAreas.Portal, StringComparison.Ordinal))
        {
            return;
        }

        foreach (ActionModel action in controller.Actions)
        {
            foreach (SelectorModel selector in action.Selectors)
            {
                selector.EndpointMetadata.Add(new AuthorizeAttribute(CrmPolicies.Portal));
            }
        }
    }
}
