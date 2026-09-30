using Crm.Modules.Tickets.Domain;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ReceiveCustomerReply;

public sealed class ReceiveCustomerReplyCommandValidator : AbstractValidator<ReceiveCustomerReplyCommand>
{
    public ReceiveCustomerReplyCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(Ticket.BodyMaxLength);
    }
}
