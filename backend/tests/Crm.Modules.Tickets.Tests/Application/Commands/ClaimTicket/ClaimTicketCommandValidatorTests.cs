using Crm.Modules.Tickets.Application.Commands.ClaimTicket;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ClaimTicket;

public sealed class ClaimTicketCommandValidatorTests
{
    private readonly ClaimTicketCommandValidator _validator = new();

    [Fact]
    public void ACommandWithAnIdIsValid() => Expect.Valid(_validator.Validate(new ClaimTicketCommand(Ids.Ticket1)));

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(_validator.Validate(new ClaimTicketCommand(Guid.Empty)), "Id");
}
