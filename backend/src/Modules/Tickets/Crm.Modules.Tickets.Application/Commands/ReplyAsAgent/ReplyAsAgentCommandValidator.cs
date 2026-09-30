using Crm.Modules.Tickets.Domain;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ReplyAsAgent;

public sealed class ReplyAsAgentCommandValidator : AbstractValidator<ReplyAsAgentCommand>
{
    public ReplyAsAgentCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(Ticket.BodyMaxLength);
    }
}
