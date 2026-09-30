using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.RemoveContact;

public sealed class RemoveContactCommandValidator : AbstractValidator<RemoveContactCommand>
{
    public RemoveContactCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.ContactId).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
