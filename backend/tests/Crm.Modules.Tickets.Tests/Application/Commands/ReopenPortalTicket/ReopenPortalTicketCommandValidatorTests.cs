using Crm.Modules.Tickets.Application.Commands.ReopenPortalTicket;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReopenPortalTicket;

public sealed class ReopenPortalTicketCommandValidatorTests
{
    private readonly ReopenPortalTicketCommandValidator _validator = new();

    [Fact]
    public void ACommandWithAnIdIsValid() => Expect.Valid(_validator.Validate(new ReopenPortalTicketCommand(Ids.Ticket1)));

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(_validator.Validate(new ReopenPortalTicketCommand(Guid.Empty)), "Id");
}
