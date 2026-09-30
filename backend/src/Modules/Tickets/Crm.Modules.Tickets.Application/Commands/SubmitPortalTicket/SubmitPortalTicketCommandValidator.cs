using Crm.Modules.Tickets.Domain;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;

public sealed class SubmitPortalTicketCommandValidator : AbstractValidator<SubmitPortalTicketCommand>
{
    public SubmitPortalTicketCommandValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(Ticket.DescriptionMaxLength);
    }
}
