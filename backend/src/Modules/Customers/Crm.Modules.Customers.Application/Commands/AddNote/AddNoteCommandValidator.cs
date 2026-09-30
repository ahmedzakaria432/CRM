using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.AddNote;

public sealed class AddNoteCommandValidator : AbstractValidator<AddNoteCommand>
{
    public AddNoteCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(CustomerNote.BodyMaxLength);
    }
}
