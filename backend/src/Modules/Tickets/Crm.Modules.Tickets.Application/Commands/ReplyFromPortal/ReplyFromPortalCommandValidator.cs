using Crm.Modules.Tickets.Domain;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ReplyFromPortal;

public sealed class ReplyFromPortalCommandValidator : AbstractValidator<ReplyFromPortalCommand>
{
    public ReplyFromPortalCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(Ticket.BodyMaxLength);
    }
}
