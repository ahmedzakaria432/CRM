using Crm.Modules.Tickets.Domain;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.AddInternalNote;

public sealed class AddInternalNoteCommandValidator : AbstractValidator<AddInternalNoteCommand>
{
    public AddInternalNoteCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(Ticket.BodyMaxLength);
    }
}
