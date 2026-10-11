using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Application.Queries.ListPortalTickets;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class ListPortalTicketsQueryValidatorTests
{
    private readonly ListPortalTicketsQueryValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(string? tab = null, string? status = null, string? search = null, int page = 1, int pageSize = 20) =>
        _validator.Validate(new ListPortalTicketsQuery(tab, status, search, page, pageSize));

    [Fact]
    public void AnEmptyQueryIsValid() => Expect.Valid(Validate());

    [Theory]
    [InlineData(null)]
    [InlineData("open")]
    [InlineData("CLOSED")]
    [InlineData("Open")]
    public void ANullOrKnownTabIsValid(string? tab) => Expect.Valid(Validate(tab: tab));

    [Theory]
    [InlineData("all")]
    [InlineData("")]
    public void AnUnknownTabIsInvalidWithTheLocalisedMessage(string tab)
    {
        ValidationResult result = Validate(tab: tab);

        Expect.Invalid(result, "Tab");
        Assert.Equal(TicketsMessageKeys.Validation.PortalTabUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Received")]
    [InlineData("inprogress")]
    [InlineData("AWAITINGYOURREPLY")]
    [InlineData("Resolved")]
    [InlineData("closed")]
    public void ANullOrPortalStatusNameInAnyCaseIsValid(string? status) => Expect.Valid(Validate(status: status));

    [Theory]
    [InlineData("Open")]
    [InlineData("PendingCustomer")]
    [InlineData("Cancelled")]
    [InlineData("")]
    public void ADomainStatusThatIsNotAPortalNameIsInvalid(string status)
    {
        ValidationResult result = Validate(status: status);

        Expect.Invalid(result, "Status");
        Assert.Equal(TicketsMessageKeys.Validation.PortalStatusUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheSearchIsLimitedToTwoHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(search: new string('s', length)).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ThePageStartsAtOne(int page, bool valid) => Assert.Equal(valid, Validate(page: page).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void ThePageSizeIsBetweenOneAndFifty(int size, bool valid) => Assert.Equal(valid, Validate(pageSize: size).IsValid);
}
