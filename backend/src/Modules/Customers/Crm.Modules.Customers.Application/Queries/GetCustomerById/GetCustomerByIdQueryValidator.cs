using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerById;

public sealed class GetCustomerByIdQueryValidator : AbstractValidator<GetCustomerByIdQuery>
{
    public GetCustomerByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
