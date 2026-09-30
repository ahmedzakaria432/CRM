using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.DeactivateCustomer;

public sealed class DeactivateCustomerCommandValidator : AbstractValidator<DeactivateCustomerCommand>
{
    public DeactivateCustomerCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
