using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketNumberTests
{
    [Theory]
    [InlineData(1, "T-000001")]
    [InlineData(7, "T-000007")]
    [InlineData(123, "T-000123")]
    [InlineData(999999, "T-999999")]
    [InlineData(1000000, "T-1000000")]
    [InlineData(123456789, "T-123456789")]
    public void ASequenceValueIsPaddedToSixDigits(long sequence, string expected)
    {
        Assert.Equal(expected, TicketNumber.FromSequence(sequence).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void ASequenceValueBelowOneIsRefused(long sequence)
    {
        Expect.DomainCode(() => TicketNumber.FromSequence(sequence), ErrorCodes.NumberInvalid);
    }

    [Fact]
    public void DigitsStayAsciiUnderTheArabicCulture()
    {
        using UiCulture culture = UiCulture.Use("ar");

        Assert.Equal("T-000042", TicketNumber.FromSequence(42).Value);
    }

    [Fact]
    public void TheLargestSequenceValueOverflowsTheMaximumLengthByOne()
    {
        string value = TicketNumber.FromSequence(long.MaxValue).Value;

        Assert.Equal(TicketNumber.MaxLength + 1, value.Length);
    }

    [Fact]
    public void ThePrefixAndMaximumLengthAreTheStoredContract()
    {
        Assert.Equal("T-", TicketNumber.Prefix);
        Assert.Equal(20, TicketNumber.MaxLength);
    }

    [Fact]
    public void NumbersWithTheSameValueAreEqual()
    {
        TicketNumber first = TicketNumber.FromSequence(5);
        TicketNumber second = TicketNumber.FromSequence(5);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void NumbersWithDifferentValuesDiffer()
    {
        Assert.NotEqual(TicketNumber.FromSequence(5), TicketNumber.FromSequence(6));
        Assert.True(TicketNumber.FromSequence(5) != TicketNumber.FromSequence(6));
    }
}
