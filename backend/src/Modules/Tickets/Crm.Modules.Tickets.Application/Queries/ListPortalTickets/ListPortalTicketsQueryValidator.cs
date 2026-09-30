using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.ListPortalTickets;

public sealed class ListPortalTicketsQueryValidator : AbstractValidator<ListPortalTicketsQuery>
{
    public ListPortalTicketsQueryValidator(ILocalizationService localization)
    {
        RuleFor(q => q.Tab).SupportedPortalTab(localization);
        RuleFor(q => q.Status).SupportedPortalStatus(localization);
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 50);
    }
}
