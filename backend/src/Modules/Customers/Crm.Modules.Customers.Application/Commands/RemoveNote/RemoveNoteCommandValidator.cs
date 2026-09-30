using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.RemoveNote;

public sealed class RemoveNoteCommandValidator : AbstractValidator<RemoveNoteCommand>
{
    public RemoveNoteCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.NoteId).NotEmpty();
    }
}
