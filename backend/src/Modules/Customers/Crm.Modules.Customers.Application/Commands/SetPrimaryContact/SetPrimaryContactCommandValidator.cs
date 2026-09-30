using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.SetPrimaryContact;

public sealed class SetPrimaryContactCommandValidator : AbstractValidator<SetPrimaryContactCommand>
{
    public SetPrimaryContactCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.ContactId).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
