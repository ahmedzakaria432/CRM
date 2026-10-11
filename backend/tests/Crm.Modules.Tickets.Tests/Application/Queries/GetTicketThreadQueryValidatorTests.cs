using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Application.Queries.GetTicketThread;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetTicketThreadQueryValidatorTests
{
    private readonly GetTicketThreadQueryValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(Guid? id = null, int? after = null, int limit = 50, IReadOnlyList<string>? visibility = null) =>
        _validator.Validate(new GetTicketThreadQuery(id ?? Ids.Ticket1, after, limit, visibility ?? []));

    [Fact]
    public void ACompleteQueryIsValid() => Expect.Valid(Validate());

    [Fact]
    public void AnEmptyTicketIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "TicketId");

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(-1, false)]
    public void TheCursorIsNullOrNonNegative(int? after, bool valid) => Assert.Equal(valid, Validate(after: after).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TheLimitIsBetweenOneAndOneHundred(int limit, bool valid) => Assert.Equal(valid, Validate(limit: limit).IsValid);

    [Theory]
    [InlineData("public")]
    [InlineData("internal")]
    [InlineData("system")]
    [InlineData("PUBLIC")]
    [InlineData("Internal")]
    [InlineData("SyStEm")]
    public void EachVisibilityInAnyCaseIsValid(string visibility) => Expect.Valid(Validate(visibility: [visibility]));

    [Fact]
    public void AnEmptyVisibilityListIsValid() => Expect.Valid(Validate(visibility: []));

    [Theory]
    [InlineData("private")]
    [InlineData("")]
    public void AnUnknownVisibilityIsInvalidAtItsIndexWithTheLocalisedMessage(string visibility)
    {
        ValidationResult result = Validate(visibility: [visibility]);

        Expect.Invalid(result, "Visibility[0]");
        Assert.Equal(TicketsMessageKeys.Validation.VisibilityUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }
}
