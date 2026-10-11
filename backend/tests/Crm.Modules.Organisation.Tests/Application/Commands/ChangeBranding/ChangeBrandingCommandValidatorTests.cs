using Crm.Modules.Organisation.Application.Commands.ChangeBranding;
using Crm.Modules.Organisation.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Organisation.Tests.Application.Commands.ChangeBranding;

public sealed class ChangeBrandingCommandValidatorTests
{
    private readonly ChangeBrandingCommandValidator _validator = new(FakeLocalization.Resources());

    private static ChangeBrandingCommand Command(
        string ar = "علامة",
        string en = "Brand",
        string primary = "#0E6655",
        string accent = "#B7791F",
        string font = "Tajawal",
        Guid? light = null,
        Guid? dark = null,
        byte[]? rowVersion = null) =>
        new(ar, en, primary, accent, font, light, dark, rowVersion ?? Ids.RowVersion());

    private ValidationResult Validate(ChangeBrandingCommand command)
    {
        using UiCulture english = UiCulture.Use("en");
        return _validator.Validate(command);
    }

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(Validate(Command()));
    }

    [Fact]
    public void LogoIdsAreOptional()
    {
        Expect.Valid(Validate(Command(light: null, dark: null)));
        Expect.Valid(Validate(Command(light: Guid.NewGuid(), dark: Guid.NewGuid())));
    }

    [Fact]
    public void EmptyDisplayNamesFail()
    {
        Expect.Invalid(Validate(Command(ar: "", en: "")), nameof(ChangeBrandingCommand.DisplayNameAr), nameof(ChangeBrandingCommand.DisplayNameEn));
    }

    [Fact]
    public void DisplayNameLengthBoundaryIsTwoHundred()
    {
        Expect.Valid(Validate(Command(ar: new string('ب', 200), en: new string('b', 200))));
        Expect.Invalid(Validate(Command(ar: new string('ب', 201))), nameof(ChangeBrandingCommand.DisplayNameAr));
        Expect.Invalid(Validate(Command(en: new string('b', 201))), nameof(ChangeBrandingCommand.DisplayNameEn));
    }

    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void APrimaryColourThatIsNotHexFails(string colour)
    {
        Expect.Invalid(Validate(Command(primary: colour)), nameof(ChangeBrandingCommand.PrimaryColour));
    }

    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void AnAccentColourThatIsNotHexFails(string colour)
    {
        Expect.Invalid(Validate(Command(accent: colour)), nameof(ChangeBrandingCommand.AccentColour));
    }

    [Fact]
    public void TheColourMessageIsTheLocalisedResourceText()
    {
        ValidationResult result = Validate(Command(primary: "blue"));

        Assert.Equal("A colour must be a hex value like #1A2B3C.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void TheColourMessageFollowsTheUiCulture()
    {
        using UiCulture arabic = UiCulture.Use("ar");

        ValidationResult result = _validator.Validate(Command(primary: "blue"));

        Assert.Equal("يجب أن يكون اللون قيمة سداسية عشرية مثل #1A2B3C.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("IbmPlexSansArabic")]
    [InlineData("NotoKufiArabic")]
    [InlineData("Tajawal")]
    public void TheThreeFontNamesAreValid(string font)
    {
        Expect.Valid(Validate(Command(font: font)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Arial")]
    [InlineData("1")]
    [InlineData("tajawal")]
    public void AnythingElseIsNotAFont(string font)
    {
        Expect.Invalid(Validate(Command(font: font)), nameof(ChangeBrandingCommand.ArabicFont));
    }

    [Fact]
    public void TheFontMessageIsTheLocalisedResourceText()
    {
        ValidationResult result = Validate(Command(font: "Arial"));

        Assert.Equal("The Arabic font must be one of IbmPlexSansArabic, NotoKufiArabic, Tajawal.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void AnEmptyRowVersionFails()
    {
        Expect.Invalid(Validate(Command(rowVersion: [])), nameof(ChangeBrandingCommand.RowVersion));
    }
}
