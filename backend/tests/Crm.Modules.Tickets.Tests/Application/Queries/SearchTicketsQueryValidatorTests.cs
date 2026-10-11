using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Application.Queries.SearchTickets;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class SearchTicketsQueryValidatorTests
{
    private readonly SearchTicketsQueryValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(
        string? queue = null, string? search = null, IReadOnlyList<string>? statuses = null, IReadOnlyList<string>? priorities = null,
        string? sort = null, int page = 1, int pageSize = 20) =>
        _validator.Validate(new SearchTicketsQuery(queue, search, statuses, priorities, null, null, null, null, null, sort, true, page, pageSize));

    [Fact]
    public void AnEmptyQueryIsValid() => Expect.Valid(Validate());

    [Fact]
    public void TheDefaultsAreDescendingPageOneAndTwenty()
    {
        var query = new SearchTicketsQuery(null, null, null, null, null, null, null, null, null, null);

        Assert.True(query.Descending);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Expect.Valid(_validator.Validate(query));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-3, false)]
    public void ThePageStartsAtOne(int page, bool valid) => Assert.Equal(valid, Validate(page: page).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ThePageSizeIsBetweenOneAndOneHundred(int size, bool valid) => Assert.Equal(valid, Validate(pageSize: size).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void TheSearchMayBeAbsentOrEmpty(string? search, bool valid) => Assert.Equal(valid, Validate(search: search).IsValid);

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheSearchIsLimitedToTwoHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(search: new string('s', length)).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("mine")]
    [InlineData("ALL")]
    [InlineData("unassigned")]
    public void AKnownQueueInAnyCaseIsValid(string? queue) => Expect.Valid(Validate(queue: queue));

    [Theory]
    [InlineData("inbox")]
    [InlineData("")]
    public void AnUnknownQueueIsInvalidWithTheLocalisedMessage(string queue)
    {
        ValidationResult result = Validate(queue: queue);

        Expect.Invalid(result, "Queue");
        Assert.Equal(TicketsMessageKeys.Validation.QueueUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("createdAt")]
    [InlineData("MODIFIEDAT")]
    [InlineData("priority")]
    [InlineData("number")]
    public void AKnownSortKeyInAnyCaseIsValid(string? sort) => Expect.Valid(Validate(sort: sort));

    [Fact]
    public void AnUnknownSortKeyIsInvalidWithTheLocalisedMessage()
    {
        ValidationResult result = Validate(sort: "price");

        Expect.Invalid(result, "Sort");
        Assert.Equal(TicketsMessageKeys.Validation.SortUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("New")]
    [InlineData("open")]
    [InlineData("PENDINGCUSTOMER")]
    [InlineData("OnHold")]
    [InlineData("Resolved")]
    [InlineData("closed")]
    [InlineData("Cancelled")]
    public void EachOfTheSevenStatusesInAnyCaseIsValid(string status) => Expect.Valid(Validate(statuses: [status]));

    [Fact]
    public void AnUnknownStatusIsInvalidAtItsIndex()
    {
        ValidationResult result = Validate(statuses: ["Open", "Archived", "1"]);

        Assert.False(result.IsValid);
        Assert.Equal(["Statuses[1]", "Statuses[2]"], result.Errors.Select(e => e.PropertyName).Order(StringComparer.Ordinal));
        Assert.All(result.Errors, e => Assert.Equal(TicketsMessageKeys.Validation.StatusUnsupported, e.ErrorMessage));
    }

    [Theory]
    [InlineData("low")]
    [InlineData("Normal")]
    [InlineData("HIGH")]
    [InlineData("urgent")]
    public void EachPriorityInAnyCaseIsValid(string priority) => Expect.Valid(Validate(priorities: [priority]));

    [Fact]
    public void AnUnknownPriorityIsInvalidAtItsIndex()
    {
        ValidationResult result = Validate(priorities: ["Low", "Critical", "2"]);

        Assert.False(result.IsValid);
        Assert.Equal(["Priorities[1]", "Priorities[2]"], result.Errors.Select(e => e.PropertyName).Order(StringComparer.Ordinal));
        Assert.All(result.Errors, e => Assert.Equal(TicketsMessageKeys.Validation.PriorityUnsupported, e.ErrorMessage));
    }

    [Fact]
    public void TheSortMessageFollowsTheUiCulture()
    {
        var validator = new SearchTicketsQueryValidator(FakeLocalization.Resources());
        var query = new SearchTicketsQuery(null, null, null, null, null, null, null, null, null, "price");

        using (UiCulture.Use("en"))
        {
            Assert.Equal("Sort must be one of createdAt, modifiedAt, priority, number.", Assert.Single(validator.Validate(query).Errors).ErrorMessage);
        }

        using (UiCulture.Use("ar"))
        {
            Assert.Contains("الترتيب", Assert.Single(validator.Validate(query).Errors).ErrorMessage, StringComparison.Ordinal);
        }
    }
}
