using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.AddContact;

// No email/phone format rule: malformed values are rejected by the value objects (422), not here (400).
public sealed class AddContactCommandValidator : AbstractValidator<AddContactCommand>
{
    public AddContactCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Contact.NameMaxLength);
        RuleFor(c => c.JobTitle).MaximumLength(Contact.JobTitleMaxLength);
        RuleFor(c => c.Email).MaximumLength(EmailAddress.MaxLength);
        RuleFor(c => c.Phone).MaximumLength(32);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
