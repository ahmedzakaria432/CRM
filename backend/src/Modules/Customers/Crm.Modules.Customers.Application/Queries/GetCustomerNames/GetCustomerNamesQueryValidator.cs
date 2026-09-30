using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerNames;

public sealed class GetCustomerNamesQueryValidator : AbstractValidator<GetCustomerNamesQuery>
{
    public GetCustomerNamesQueryValidator()
    {
        RuleFor(q => q.CustomerIds).NotNull().Must(ids => ids.Count <= 100);
    }
}
