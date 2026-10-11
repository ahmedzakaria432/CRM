using Crm.Modules.Tickets.Application.Commands.AssignTicket;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.AssignTicket;

public sealed class AssignTicketCommandValidatorTests
{
    private readonly AssignTicketCommandValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, Guid? agent = null, byte[]? rowVersion = null) =>
        _validator.Validate(new AssignTicketCommand(id ?? Ids.Ticket1, agent ?? Ids.Agent1, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyAgentIsInvalid() => Expect.Invalid(Validate(agent: Guid.Empty), "AgentId");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");
}
