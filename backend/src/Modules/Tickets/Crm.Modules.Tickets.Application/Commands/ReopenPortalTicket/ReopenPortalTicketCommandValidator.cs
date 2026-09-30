using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ReopenPortalTicket;

public sealed class ReopenPortalTicketCommandValidator : AbstractValidator<ReopenPortalTicketCommand>
{
    public ReopenPortalTicketCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
