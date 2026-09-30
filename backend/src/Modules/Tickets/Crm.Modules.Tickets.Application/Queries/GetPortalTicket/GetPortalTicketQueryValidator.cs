using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.GetPortalTicket;

public sealed class GetPortalTicketQueryValidator : AbstractValidator<GetPortalTicketQuery>
{
    public GetPortalTicketQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
