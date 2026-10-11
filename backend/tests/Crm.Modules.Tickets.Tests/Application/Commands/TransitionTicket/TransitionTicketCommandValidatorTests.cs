using Crm.Modules.Tickets.Application.Commands.TransitionTicket;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.TransitionTicket;

public sealed class TransitionTicketCommandValidatorTests
{
    private readonly TransitionTicketCommandValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(Guid? id = null, string action = "Open", byte[]? rowVersion = null) =>
        _validator.Validate(new TransitionTicketCommand(id ?? Ids.Ticket1, action, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");

    [Theory]
    [InlineData("Open")]
    [InlineData("AwaitCustomer")]
    [InlineData("PutOnHold")]
    [InlineData("Resolve")]
    [InlineData("Close")]
    [InlineData("Reopen")]
    [InlineData("Cancel")]
    [InlineData("open")]
    [InlineData("CANCEL")]
    [InlineData("awaitcustomer")]
    public void EachOfTheSevenActionsInAnyCaseIsValid(string action) => Expect.Valid(Validate(action: action));

    [Theory]
    [InlineData("Assign")]
    [InlineData("Claim")]
    [InlineData("Reply")]
    [InlineData("1")]
    [InlineData("Explode")]
    public void AnythingElseIsInvalidWithTheLocalisedMessage(string action)
    {
        ValidationResult result = Validate(action: action);

        Expect.Invalid(result, "Action");
        Assert.Equal(TicketsMessageKeys.Validation.ActionUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void AnEmptyActionIsInvalid()
    {
        ValidationResult result = Validate(action: "");

        Expect.Invalid(result, "Action");
    }
}
