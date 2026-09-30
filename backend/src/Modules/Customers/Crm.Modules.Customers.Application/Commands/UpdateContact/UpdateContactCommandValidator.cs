using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.UpdateContact;

public sealed class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.ContactId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Contact.NameMaxLength);
        RuleFor(c => c.JobTitle).MaximumLength(Contact.JobTitleMaxLength);
        RuleFor(c => c.Email).MaximumLength(EmailAddress.MaxLength);
        RuleFor(c => c.Phone).MaximumLength(32);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
