namespace Crm.SharedKernel.Tests;

public sealed class StronglyTypedIdTests
{
    private static readonly Guid Raw = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void ToStringOfADerivedRecordIsTheSynthesizedRecordFormat()
    {
        // Pins the observed behaviour: StronglyTypedId overrides ToString to return the bare guid, but every
        // derived record gets its own compiler-synthesized ToString (the base override is not sealed), so the
        // guid string is never what callers see. Recorded as a Follow-up of Story 60 (UT-01).
        var text = new CustomerId(Raw).ToString();

        Assert.Equal("CustomerId { Value = " + Raw + " }", text);
    }

    [Fact]
    public void SameTypeAndValueAreEqual()
    {
        Assert.Equal(new CustomerId(Raw), new CustomerId(Raw));
        Assert.Equal(new CustomerId(Raw).GetHashCode(), new CustomerId(Raw).GetHashCode());
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        Assert.NotEqual(new CustomerId(Raw), new CustomerId(Guid.NewGuid()));
    }

    [Fact]
    public void DifferentIdTypesWithTheSameGuidAreNotEqual()
    {
        StronglyTypedId customer = new CustomerId(Raw);
        StronglyTypedId ticket = new TicketId(Raw);

        Assert.NotEqual(customer, ticket);
        Assert.False(customer.Equals(ticket));
    }

    [Fact]
    public void IdsWorkAsDictionaryKeys()
    {
        var map = new Dictionary<CustomerId, string> { [new CustomerId(Raw)] = "found" };

        Assert.Equal("found", map[new CustomerId(Raw)]);
    }

    [Fact]
    public void WithCreatesANewIdAndLeavesTheOriginal()
    {
        var original = new CustomerId(Raw);
        var other = Guid.NewGuid();

        var copy = original with { Value = other };

        Assert.Equal(other, copy.Value);
        Assert.Equal(Raw, original.Value);
    }

    private sealed record CustomerId(Guid Value) : StronglyTypedId(Value);

    private sealed record TicketId(Guid Value) : StronglyTypedId(Value);
}
