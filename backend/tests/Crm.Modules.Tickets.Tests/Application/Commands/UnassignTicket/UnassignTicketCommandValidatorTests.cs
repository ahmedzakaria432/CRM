using Crm.Modules.Tickets.Application.Commands.UnassignTicket;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.UnassignTicket;

public sealed class UnassignTicketCommandValidatorTests
{
    private readonly UnassignTicketCommandValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, byte[]? rowVersion = null) =>
        _validator.Validate(new UnassignTicketCommand(id ?? Ids.Ticket1, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");
}
