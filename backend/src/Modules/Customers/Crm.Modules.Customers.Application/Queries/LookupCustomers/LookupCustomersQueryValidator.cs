using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.LookupCustomers;

public sealed class LookupCustomersQueryValidator : AbstractValidator<LookupCustomersQuery>
{
    public LookupCustomersQueryValidator()
    {
        RuleFor(q => q.Take).InclusiveBetween(1, 25);
        RuleFor(q => q.Search).MaximumLength(200);
    }
}
