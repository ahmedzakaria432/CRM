using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Queries.ListTicketCategories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Tickets.Presentation.Controllers.Agent;

/// <summary>The category picker: active categories only (a deactivated one disappears here but stays on its tickets).</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TicketCategoriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryDto>>> List([FromQuery] Guid? departmentId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListTicketCategoriesQuery(departmentId), cancellationToken));
}
