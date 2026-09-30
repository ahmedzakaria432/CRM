using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.SearchTickets;

public sealed class SearchTicketsQueryValidator : AbstractValidator<SearchTicketsQuery>
{
    public SearchTicketsQueryValidator(ILocalizationService localization)
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.Queue).SupportedQueue(localization);
        RuleFor(q => q.Sort).SupportedSort(localization);
        RuleForEach(q => q.Statuses).SupportedStatus(localization);
        RuleForEach(q => q.Priorities).SupportedPriority(localization);
    }
}
