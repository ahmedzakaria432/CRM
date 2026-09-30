using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.AssignTicket;

public sealed class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.AgentId).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
