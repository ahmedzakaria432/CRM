using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.TransferTicket;

public sealed class TransferTicketCommandValidator : AbstractValidator<TransferTicketCommand>
{
    public TransferTicketCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.DepartmentId).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
