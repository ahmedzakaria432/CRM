using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketById;

public sealed class GetTicketByIdQueryHandler(IRepository<Ticket, TicketId> tickets, TicketDtoAssembler assembler)
    : IRequestHandler<GetTicketByIdQuery, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.FirstOrDefaultAsync(new TicketByIdSpecification(new TicketId(request.Id)), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        return Result.Success(await assembler.ToDtoAsync(ticket, cancellationToken));
    }
}
