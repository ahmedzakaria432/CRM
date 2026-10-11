using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class LocalizedTextTests
{
    [Fact]
    public void CreateTrimsBothLanguages()
    {
        LocalizedText text = LocalizedText.Create("  الرياض ", " Riyadh  ");

        Assert.Equal("الرياض", text.Arabic);
        Assert.Equal("Riyadh", text.English);
    }

    [Theory]
    [InlineData("", "Riyadh")]
    [InlineData("   ", "Riyadh")]
    [InlineData("الرياض", "")]
    [InlineData("الرياض", "   ")]
    public void CreateRejectsABlankLanguage(string arabic, string english)
    {
        var exception = Assert.Throws<DomainException>(() => LocalizedText.Create(arabic, english));

        Assert.Equal("Organisation.NameRequired", exception.Error.Code);
    }

    [Fact]
    public void CreateAcceptsTwoHundredCharactersInEachLanguage()
    {
        LocalizedText text = LocalizedText.Create(new string('ب', 200), new string('b', 200));

        Assert.Equal(200, text.Arabic.Length);
        Assert.Equal(200, text.English.Length);
    }

    [Fact]
    public void CreateRejectsAnArabicNameOverTwoHundredCharacters()
    {
        var exception = Assert.Throws<DomainException>(() => LocalizedText.Create(new string('ب', 201), "ok"));

        Assert.Equal("Organisation.NameTooLong", exception.Error.Code);
    }

    [Fact]
    public void CreateRejectsAnEnglishNameOverTwoHundredCharacters()
    {
        var exception = Assert.Throws<DomainException>(() => LocalizedText.Create("ok", new string('b', 201)));

        Assert.Equal("Organisation.NameTooLong", exception.Error.Code);
    }

    [Fact]
    public void LengthIsMeasuredAfterTrimming()
    {
        LocalizedText text = LocalizedText.Create("  " + new string('ب', 200) + "  ", "ok");

        Assert.Equal(200, text.Arabic.Length);
    }

    [Fact]
    public void CreateRejectsNullArguments()
    {
        Func<string, string, LocalizedText> create = LocalizedText.Create;

        NullArguments.AssertThrowsArgumentNull(create, null, "x");
        NullArguments.AssertThrowsArgumentNull(create, "x", null);
    }

    [Fact]
    public void TextsAreEqualWhenBothLanguagesMatch()
    {
        Assert.Equal(LocalizedText.Create("أ", "A"), LocalizedText.Create(" أ ", "A "));
        Assert.True(LocalizedText.Create("أ", "A") == LocalizedText.Create("أ", "A"));
    }

    [Fact]
    public void TextsDifferWhenEitherLanguageDiffers()
    {
        LocalizedText original = LocalizedText.Create("أ", "A");

        Assert.NotEqual(original, LocalizedText.Create("ب", "A"));
        Assert.NotEqual(original, LocalizedText.Create("أ", "B"));
    }

    [Fact]
    public void ArabicTextRoundTrips()
    {
        const string arabic = "مركز الدعم التجريبي";

        Assert.Equal(arabic, LocalizedText.Create(arabic, "Demo").Arabic);
    }
}
