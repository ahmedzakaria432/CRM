using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.ReactivateCustomer;

public sealed class ReactivateCustomerCommandValidator : AbstractValidator<ReactivateCustomerCommand>
{
    public ReactivateCustomerCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
