using Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandValidatorTests
{
    private readonly ChangeTicketPriorityCommandValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(Guid? id = null, string priority = "High", byte[]? rowVersion = null) =>
        _validator.Validate(new ChangeTicketPriorityCommand(id ?? Ids.Ticket1, priority, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");

    [Theory]
    [InlineData("Low")]
    [InlineData("normal")]
    [InlineData("HIGH")]
    [InlineData("high")]
    public void AKnownPriorityInAnyCaseIsValid(string priority) => Expect.Valid(Validate(priority: priority));

    [Theory]
    [InlineData("")]
    [InlineData("Critical")]
    [InlineData("2")]
    public void AnEmptyUnknownOrNumericPriorityIsInvalid(string priority) => Expect.Invalid(Validate(priority: priority), "Priority");

    [Fact]
    public void TheMessageIsTheLocalisedPriorityKey()
    {
        ValidationResult result = Validate(priority: "Critical");

        Assert.Equal(TicketsMessageKeys.Validation.PriorityUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }
}
