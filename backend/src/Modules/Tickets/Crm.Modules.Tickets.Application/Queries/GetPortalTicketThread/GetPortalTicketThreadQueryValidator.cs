using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.GetPortalTicketThread;

public sealed class GetPortalTicketThreadQueryValidator : AbstractValidator<GetPortalTicketThreadQuery>
{
    public GetPortalTicketThreadQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
        RuleFor(q => q.After).GreaterThanOrEqualTo(0).When(q => q.After is not null);
        RuleFor(q => q.Limit).InclusiveBetween(1, 100);
    }
}
