using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using Crm.Modules.Identity.Tests.Support;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Modules.Identity.Tests.Application.Validation;

public sealed class IdentityRulesTests
{
    private sealed record Holder(string Value);

    private static InlineValidator<Holder> Password(ILocalizationService localization) =>
        new() { v => v.RuleFor(h => h.Value).StaffPassword(localization) };

    private static InlineValidator<Holder> Locale(ILocalizationService localization) =>
        new() { v => v.RuleFor(h => h.Value).SupportedLocale(localization) };

    private static ValidationResult CheckPassword(string value) => Password(FakeLocalization.Keys()).Validate(new Holder(value));

    [Theory]
    [InlineData("abcdefghi1")]
    [InlineData("Abcdefgh12")]
    [InlineData("a1bcdefghij")]
    public void TenToOneHundredTwentyEightWithALetterAndADigitIsValid(string value) => Expect.Valid(CheckPassword(value));

    [Fact]
    public void ExactlyTenCharactersIsValidAndNineIsNot()
    {
        Expect.Valid(CheckPassword("abcdefghi1"));
        Assert.False(CheckPassword("abcdefgh1").IsValid);
    }

    [Fact]
    public void ExactlyOneHundredTwentyEightCharactersIsValidAndOneHundredTwentyNineIsNot()
    {
        Expect.Valid(CheckPassword(new string('a', 127) + "1"));
        Assert.False(CheckPassword(new string('a', 128) + "1").IsValid);
    }

    [Fact]
    public void AnEmptyPasswordFails() => Assert.False(CheckPassword(string.Empty).IsValid);

    [Fact]
    public void AWhitespaceOnlyPasswordFails() => Assert.False(CheckPassword("          ").IsValid);

    [Fact]
    public void ANoDigitPasswordFailsWithTheDigitMessageKey()
    {
        ValidationResult result = CheckPassword("abcdefghij");

        Assert.Equal("Identity.Validation.PasswordNeedsDigit", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void ANoLetterPasswordFailsWithTheLetterMessageKey()
    {
        ValidationResult result = CheckPassword("0123456789");

        Assert.Equal("Identity.Validation.PasswordNeedsLetter", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void ArabicLettersOnlyPlusDigitsFailTheLetterRuleBecauseTheRuleIsAsciiOnly()
    {
        ValidationResult result = CheckPassword("كلمةالمرور123");

        Assert.Equal("Identity.Validation.PasswordNeedsLetter", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void ABadPasswordReportsSeveralMessagesBecauseTheRulesDoNotStopAtTheFirstFailure()
    {
        ValidationResult result = CheckPassword("short");

        Assert.True(result.Errors.Count >= 2);
    }

    [Fact]
    public void PasswordMessagesAreEnglishByDefault()
    {
        using UiCulture culture = UiCulture.Use("en");

        ValidationResult result = Password(FakeLocalization.Resources()).Validate(new Holder("abcdefghij"));

        Assert.Equal("The password needs at least one digit.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void PasswordMessagesAreArabicUnderTheArabicCulture()
    {
        using UiCulture culture = UiCulture.Use("ar");

        ValidationResult result = Password(FakeLocalization.Resources()).Validate(new Holder("0123456789"));

        Assert.Equal("يجب أن تحتوي كلمة المرور على حرف واحد على الأقل.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void StaffPasswordGuardsNullArguments()
    {
        var holder = new InlineValidator<Holder>();

        NullArguments.AssertThrowsArgumentNull(IdentityRules.StaffPassword<Holder>, null, FakeLocalization.Keys());
        NullArguments.AssertThrowsArgumentNull(IdentityRules.StaffPassword<Holder>, holder.RuleFor(h => h.Value), null);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void SupportedLocalesAreValid(string value) => Expect.Valid(Locale(FakeLocalization.Keys()).Validate(new Holder(value)));

    [Theory]
    [InlineData("EN")]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("en-US")]
    public void UnsupportedLocalesFailWithTheLocalisedMessage(string value)
    {
        ValidationResult result = Locale(FakeLocalization.Keys()).Validate(new Holder(value));

        Assert.Equal("Identity.Validation.LocaleUnsupported", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void LocaleMessagesAreEnglishAndArabic()
    {
        using (UiCulture.Use("en"))
        {
            Assert.Equal("Preferred language must be en or ar.", Assert.Single(Locale(FakeLocalization.Resources()).Validate(new Holder("fr")).Errors).ErrorMessage);
        }

        using (UiCulture.Use("ar"))
        {
            Assert.Equal("يجب أن تكون اللغة المفضلة en أو ar.", Assert.Single(Locale(FakeLocalization.Resources()).Validate(new Holder("fr")).Errors).ErrorMessage);
        }
    }

    [Fact]
    public void SupportedLocaleGuardsNullArguments()
    {
        var holder = new InlineValidator<Holder>();

        NullArguments.AssertThrowsArgumentNull(IdentityRules.SupportedLocale<Holder>, null, FakeLocalization.Keys());
        NullArguments.AssertThrowsArgumentNull(IdentityRules.SupportedLocale<Holder>, holder.RuleFor(h => h.Value), null);
    }
}
