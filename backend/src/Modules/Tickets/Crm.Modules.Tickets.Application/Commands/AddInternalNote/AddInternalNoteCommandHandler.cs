using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.AddInternalNote;

public sealed class AddInternalNoteCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler,
    IDateTime dateTime)
    : IRequestHandler<AddInternalNoteCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(AddInternalNoteCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        // A note is allowed in every status, so there is no refusal check. It publishes no integration event.
        _ = ticket.AddInternalNote(request.Body, CurrentActor.Id(currentUser), dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await assembler.ToDtoAsync(ticket, cancellationToken));
    }
}
