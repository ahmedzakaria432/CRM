using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Specifications;

public sealed class TicketByIdSpecification : Specification<Ticket>
{
    public TicketByIdSpecification(TicketId id)
    {
        Where(t => t.Id == id);
        AsReadOnly();
    }
}
