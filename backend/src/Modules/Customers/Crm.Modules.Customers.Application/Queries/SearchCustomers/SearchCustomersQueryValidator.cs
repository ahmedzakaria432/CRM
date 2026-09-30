using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Customers.Application.Validation;
using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.SearchCustomers;

public sealed class SearchCustomersQueryValidator : AbstractValidator<SearchCustomersQuery>
{
    public SearchCustomersQueryValidator(ILocalizationService localization)
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.Status).Must(s => s is null || Enum.GetNames<CustomerStatus>().Contains(s, StringComparer.OrdinalIgnoreCase));
        RuleFor(q => q.Type).Must(t => t is null || Enum.GetNames<CustomerType>().Contains(t, StringComparer.OrdinalIgnoreCase));
        RuleFor(q => q.Sort).SupportedSort(localization);
    }
}
