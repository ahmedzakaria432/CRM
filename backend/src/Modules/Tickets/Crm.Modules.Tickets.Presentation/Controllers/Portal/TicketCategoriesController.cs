using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Queries.ListPortalTicketCategories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Tickets.Presentation.Controllers.Portal;

/// <summary>The categories a portal customer may pick when raising a request (Story 47): active and customer-visible, of
/// the department that owns the customer.</summary>
[Area(CrmAreas.Portal)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TicketCategoriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PortalTicketCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalTicketCategoryDto>>> List(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListPortalTicketCategoriesQuery(), cancellationToken));
}
