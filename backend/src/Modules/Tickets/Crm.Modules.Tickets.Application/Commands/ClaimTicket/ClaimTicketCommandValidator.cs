using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ClaimTicket;

public sealed class ClaimTicketCommandValidator : AbstractValidator<ClaimTicketCommand>
{
    public ClaimTicketCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
