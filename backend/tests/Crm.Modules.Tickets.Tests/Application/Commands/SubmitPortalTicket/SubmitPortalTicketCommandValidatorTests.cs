using Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.SubmitPortalTicket;

public sealed class SubmitPortalTicketCommandValidatorTests
{
    private readonly SubmitPortalTicketCommandValidator _validator = new();

    private ValidationResult Validate(string subject = "Subject", string description = "Description") =>
        _validator.Validate(new SubmitPortalTicketCommand(subject, description, null));

    [Fact]
    public void ACompleteCommandIsValid() => Assert.True(Validate().IsValid);

    [Theory]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("s", true)]
    public void TheSubjectIsRequired(string subject, bool valid) => Assert.Equal(valid, Validate(subject: subject).IsValid);

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheSubjectIsLimitedToTwoHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(subject: new string('s', length)).IsValid);

    [Theory]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("d", true)]
    public void TheDescriptionIsRequired(string description, bool valid) => Assert.Equal(valid, Validate(description: description).IsValid);

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void TheDescriptionIsLimitedToFourThousandCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(description: new string('d', length)).IsValid);

    [Fact]
    public void TheFailingPropertyIsNamed()
    {
        Assert.Equal("Subject", Assert.Single(Validate(subject: "").Errors.Select(e => e.PropertyName).Distinct()));
        Assert.Equal("Description", Assert.Single(Validate(description: "").Errors.Select(e => e.PropertyName).Distinct()));
    }
}
