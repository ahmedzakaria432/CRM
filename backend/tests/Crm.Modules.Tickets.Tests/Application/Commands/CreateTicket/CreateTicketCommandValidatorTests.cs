using Crm.Modules.Tickets.Application.Commands.CreateTicket;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.CreateTicket;

public sealed class CreateTicketCommandValidatorTests
{
    private readonly CreateTicketCommandValidator _validator = new(FakeLocalization.Keys());

    private static CreateTicketCommand Command(
        Guid? customer = null, string subject = "Printer", string description = "It is broken.", string? priority = null) =>
        new(customer ?? Ids.Customer1, null, subject, description, priority, null, null);

    private ValidationResult Validate(CreateTicketCommand command) => _validator.Validate(command);

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate(Command()));

    [Fact]
    public void AnEmptyCustomerIsInvalid() => Expect.Invalid(Validate(Command(customer: Guid.Empty)), "CustomerId");

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a", true)]
    public void TheSubjectIsRequired(string subject, bool valid)
    {
        ValidationResult result = Validate(Command(subject: subject));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "Subject");
        }
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheSubjectIsLimitedToTwoHundredCharacters(int length, bool valid)
    {
        ValidationResult result = Validate(Command(subject: new string('s', length)));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("d", true)]
    public void TheDescriptionIsRequired(string description, bool valid)
    {
        Assert.Equal(valid, Validate(Command(description: description)).IsValid);
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void TheDescriptionIsLimitedToFourThousandCharacters(int length, bool valid)
    {
        ValidationResult result = Validate(Command(description: new string('d', length)));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("low")]
    [InlineData("NORMAL")]
    [InlineData("Urgent")]
    [InlineData("High")]
    public void ANullOrKnownPriorityIsValid(string? priority) => Expect.Valid(Validate(Command(priority: priority)));

    [Theory]
    [InlineData("")]
    [InlineData("Critical")]
    [InlineData("1")]
    [InlineData("2")]
    public void AnUnknownOrNumericPriorityIsInvalidWithTheLocalisedMessage(string priority)
    {
        ValidationResult result = Validate(Command(priority: priority));

        Expect.Invalid(result, "Priority");
        Assert.Equal(TicketsMessageKeys.Validation.PriorityUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void ContactCategoryAndDepartmentAreUnconstrained()
    {
        var command = new CreateTicketCommand(Ids.Customer1, Guid.Empty, "s", "d", null, Guid.Empty, Guid.Empty);

        Expect.Valid(Validate(command));
    }

    [Fact]
    public void ThePriorityMessageFollowsTheUiCulture()
    {
        var validator = new CreateTicketCommandValidator(FakeLocalization.Resources());

        using (UiCulture.Use("en"))
        {
            Assert.Equal("Priority must be Low, Normal, High or Urgent.", Assert.Single(validator.Validate(Command(priority: "x")).Errors).ErrorMessage);
        }

        using (UiCulture.Use("ar"))
        {
            string arabic = Assert.Single(validator.Validate(Command(priority: "x")).Errors).ErrorMessage;
            Assert.Contains("الأولوية", arabic, StringComparison.Ordinal);
        }
    }
}
