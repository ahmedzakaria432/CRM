using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class LocalizedTextTests
{
    [Fact]
    public void BothSidesAreTrimmed()
    {
        LocalizedText text = LocalizedText.Create("  استفسار  ", "  Enquiry  ");

        Assert.Equal("استفسار", text.Arabic);
        Assert.Equal("Enquiry", text.English);
    }

    [Fact]
    public void ArabicRoundTrips()
    {
        Assert.Equal("مشكلة تقنية", LocalizedText.Create("مشكلة تقنية", "Technical issue").Arabic);
    }

    [Fact]
    public void ANullArabicSideIsRefused()
    {
        NullArguments.AssertThrowsArgumentNull(LocalizedText.Create, null, "Enquiry");
    }

    [Fact]
    public void ANullEnglishSideIsRefused()
    {
        NullArguments.AssertThrowsArgumentNull(LocalizedText.Create, "استفسار", null);
    }

    [Theory]
    [InlineData("", "Enquiry")]
    [InlineData("   ", "Enquiry")]
    [InlineData("استفسار", "")]
    [InlineData("استفسار", "   ")]
    [InlineData("", "")]
    public void ABlankSideIsRefusedAndNeverFilledFromTheOther(string arabic, string english)
    {
        Expect.DomainCode(() => LocalizedText.Create(arabic, english), ErrorCodes.CategoryNameRequired);
    }

    [Fact]
    public void TwoHundredCharactersAreAcceptedOnBothSides()
    {
        LocalizedText text = LocalizedText.Create(new string('ع', 200), new string('e', 200));

        Assert.Equal(200, text.Arabic.Length);
        Assert.Equal(200, text.English.Length);
    }

    [Fact]
    public void ArabicLongerThanTwoHundredIsRefused()
    {
        Expect.DomainCode(() => LocalizedText.Create(new string('ع', 201), "Enquiry"), ErrorCodes.CategoryNameTooLong);
    }

    [Fact]
    public void EnglishLongerThanTwoHundredIsRefused()
    {
        Expect.DomainCode(() => LocalizedText.Create("استفسار", new string('e', 201)), ErrorCodes.CategoryNameTooLong);
    }

    [Fact]
    public void TheLengthIsCheckedAfterTheTrim()
    {
        LocalizedText text = LocalizedText.Create("  " + new string('ع', 200) + "  ", new string('e', 200));

        Assert.Equal(200, text.Arabic.Length);
    }

    [Fact]
    public void TheMaximumLengthIsTwoHundred()
    {
        Assert.Equal(200, LocalizedText.MaxLength);
    }

    [Fact]
    public void ValuesWithBothSidesEqualAreEqual()
    {
        Assert.Equal(LocalizedText.Create("أ", "a"), LocalizedText.Create("أ", "a"));
        Assert.NotEqual(LocalizedText.Create("أ", "a"), LocalizedText.Create("أ", "b"));
        Assert.NotEqual(LocalizedText.Create("أ", "a"), LocalizedText.Create("ب", "a"));
    }
}
