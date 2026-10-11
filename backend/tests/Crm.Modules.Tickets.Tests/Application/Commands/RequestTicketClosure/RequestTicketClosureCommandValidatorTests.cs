using Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands.RequestTicketClosure;

public sealed class RequestTicketClosureCommandValidatorTests
{
    private readonly RequestTicketClosureCommandValidator _validator = new();

    [Fact]
    public void ACommandWithAnIdIsValid() => Expect.Valid(_validator.Validate(new RequestTicketClosureCommand(Ids.Ticket1)));

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(_validator.Validate(new RequestTicketClosureCommand(Guid.Empty)), "Id");
}
