using Crm.Modules.Tickets.Application.Queries.GetCategory;
using Crm.Modules.Tickets.Application.Queries.GetPortalTicket;
using Crm.Modules.Tickets.Application.Queries.GetTicketById;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

/// <summary>The three one-id query validators: ticket, portal ticket and category.</summary>
public sealed class GetTicketByIdQueryValidatorTests
{
    [Fact]
    public void AGetTicketByIdQueryWithAnIdIsValid() =>
        Expect.Valid(new GetTicketByIdQueryValidator().Validate(new GetTicketByIdQuery(Ids.Ticket1)));

    [Fact]
    public void AGetTicketByIdQueryWithAnEmptyIdIsInvalid() =>
        Expect.Invalid(new GetTicketByIdQueryValidator().Validate(new GetTicketByIdQuery(Guid.Empty)), "Id");

    [Fact]
    public void AGetCategoryQueryWithAnIdIsValid() =>
        Expect.Valid(new GetCategoryQueryValidator().Validate(new GetCategoryQuery(Ids.Category1)));

    [Fact]
    public void AGetCategoryQueryWithAnEmptyIdIsInvalid() =>
        Expect.Invalid(new GetCategoryQueryValidator().Validate(new GetCategoryQuery(Guid.Empty)), "Id");

    [Fact]
    public void AGetPortalTicketQueryWithAnIdIsValid() =>
        Expect.Valid(new GetPortalTicketQueryValidator().Validate(new GetPortalTicketQuery(Ids.Ticket1)));

    [Fact]
    public void AGetPortalTicketQueryWithAnEmptyIdIsInvalid() =>
        Expect.Invalid(new GetPortalTicketQueryValidator().Validate(new GetPortalTicketQuery(Guid.Empty)), "Id");
}
