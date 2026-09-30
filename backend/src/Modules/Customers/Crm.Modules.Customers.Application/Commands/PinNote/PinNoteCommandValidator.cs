using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.PinNote;

public sealed class PinNoteCommandValidator : AbstractValidator<PinNoteCommand>
{
    public PinNoteCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.NoteId).NotEmpty();
    }
}
