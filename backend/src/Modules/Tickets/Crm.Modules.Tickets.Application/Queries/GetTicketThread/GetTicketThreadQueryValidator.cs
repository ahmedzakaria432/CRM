using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketThread;

public sealed class GetTicketThreadQueryValidator : AbstractValidator<GetTicketThreadQuery>
{
    public GetTicketThreadQueryValidator(ILocalizationService localization)
    {
        RuleFor(q => q.TicketId).NotEmpty();
        RuleFor(q => q.After).GreaterThanOrEqualTo(0).When(q => q.After is not null);
        RuleFor(q => q.Limit).InclusiveBetween(1, 100);
        RuleForEach(q => q.Visibility).SupportedVisibility(localization);
    }
}
