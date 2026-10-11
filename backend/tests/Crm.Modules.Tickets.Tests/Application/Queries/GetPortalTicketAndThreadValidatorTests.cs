using Crm.Modules.Tickets.Application.Queries.GetPortalTicketThread;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetPortalTicketAndThreadValidatorTests
{
    private readonly GetPortalTicketThreadQueryValidator _validator = new();

    private ValidationResult Validate(Guid? id = null, int? after = null, int limit = 50) =>
        _validator.Validate(new GetPortalTicketThreadQuery(id ?? Ids.Ticket1, after, limit));

    [Fact]
    public void ACompleteQueryIsValid() => Expect.Valid(Validate());

    [Fact]
    public void TheDefaultLimitIsFifty() => Assert.Equal(50, new GetPortalTicketThreadQuery(Ids.Ticket1, null).Limit);

    [Fact]
    public void AnEmptyIdIsInvalid() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void TheCursorIsNullOrNonNegative(int? after, bool valid) => Assert.Equal(valid, Validate(after: after).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TheLimitIsBetweenOneAndOneHundred(int limit, bool valid) => Assert.Equal(valid, Validate(limit: limit).IsValid);
}
