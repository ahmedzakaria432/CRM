using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketById;

public sealed class GetTicketByIdQueryValidator : AbstractValidator<GetTicketByIdQuery>
{
    public GetTicketByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
