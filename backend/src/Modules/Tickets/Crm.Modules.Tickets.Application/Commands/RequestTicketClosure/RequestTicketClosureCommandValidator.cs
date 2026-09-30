using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;

public sealed class RequestTicketClosureCommandValidator : AbstractValidator<RequestTicketClosureCommand>
{
    public RequestTicketClosureCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
