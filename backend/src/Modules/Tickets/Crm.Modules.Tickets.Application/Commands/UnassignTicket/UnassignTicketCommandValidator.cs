using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.UnassignTicket;

public sealed class UnassignTicketCommandValidator : AbstractValidator<UnassignTicketCommand>
{
    public UnassignTicketCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
