using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Localization;
using Crm.Modules.Organisation.Application.Validation;
using Crm.Modules.Organisation.Tests.Support;
using FluentValidation;

namespace Crm.Modules.Organisation.Tests.Application;

public sealed class OrganisationRulesTests
{
    private sealed record Holder(string Value);

    private static InlineValidator<Holder> Validator(Action<IRuleBuilder<Holder, string>, ILocalizationService> configure)
    {
        var validator = new InlineValidator<Holder>();
        configure(validator.RuleFor(h => h.Value), FakeLocalization.Keys());
        return validator;
    }

    private static InlineValidator<Holder> HexValidator() => Validator((rule, loc) => rule.HexColour(loc));

    private static InlineValidator<Holder> FontValidator() => Validator((rule, loc) => rule.SupportedArabicFont(loc));

    private static InlineValidator<Holder> CodeValidator() => Validator((rule, loc) => rule.OrganisationCode(loc));

    [Theory]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#0E6655")]
    [InlineData("#aBcDeF")]
    public void HexColourAcceptsSixDigitHex(string value)
    {
        Expect.Valid(HexValidator().Validate(new Holder(value)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0E6655")]
    [InlineData("#0E665")]
    [InlineData("#0E66555")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    [InlineData(" #0E6655")]
    public void HexColourRejectsEverythingElse(string value)
    {
        Expect.Invalid(HexValidator().Validate(new Holder(value)), nameof(Holder.Value));
    }

    [Fact]
    public void HexColourMessageComesFromTheLocalizationKey()
    {
        var result = HexValidator().Validate(new Holder("x"));

        Assert.Equal(OrganisationMessageKeys.Validation.ColourInvalid, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("IbmPlexSansArabic")]
    [InlineData("NotoKufiArabic")]
    [InlineData("Tajawal")]
    public void SupportedArabicFontAcceptsTheThreeNames(string value)
    {
        Expect.Valid(FontValidator().Validate(new Holder(value)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Arial")]
    [InlineData("tajawal")]
    [InlineData("0")]
    [InlineData("2")]
    public void SupportedArabicFontRejectsAnythingElseIncludingNumbers(string value)
    {
        Expect.Invalid(FontValidator().Validate(new Holder(value)), nameof(Holder.Value));
    }

    [Fact]
    public void SupportedArabicFontMessageComesFromTheLocalizationKey()
    {
        var result = FontValidator().Validate(new Holder("Arial"));

        Assert.Equal(OrganisationMessageKeys.Validation.ArabicFontUnsupported, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Demo")]
    [InlineData("A-1")]
    [InlineData("12")]
    public void OrganisationCodeAcceptsTwoToTwentyLettersDigitsAndHyphens(string value)
    {
        Expect.Valid(CodeValidator().Validate(new Holder(value)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a b")]
    [InlineData("a_b")]
    [InlineData(" ab")]
    public void OrganisationCodeRejectsEverythingElse(string value)
    {
        Expect.Invalid(CodeValidator().Validate(new Holder(value)), nameof(Holder.Value));
    }

    [Fact]
    public void OrganisationCodeLengthBoundaryIsTwenty()
    {
        Expect.Valid(CodeValidator().Validate(new Holder(new string('a', 20))));
        Expect.Invalid(CodeValidator().Validate(new Holder(new string('a', 21))), nameof(Holder.Value));
    }

    [Fact]
    public void OrganisationCodeMessageComesFromTheLocalizationKey()
    {
        var result = CodeValidator().Validate(new Holder("a"));

        Assert.Equal(OrganisationMessageKeys.Validation.OrganisationCodeInvalid, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void EveryRuleRejectsANullRuleOrLocalization()
    {
        var validator = new InlineValidator<Holder>();
        IRuleBuilder<Holder, string> rule = validator.RuleFor(h => h.Value);
        ILocalizationService localization = FakeLocalization.Keys();
        Func<IRuleBuilder<Holder, string>, ILocalizationService, IRuleBuilderOptions<Holder, string>>[] rules =
        [
            OrganisationRules.HexColour,
            OrganisationRules.SupportedArabicFont,
            OrganisationRules.OrganisationCode,
        ];

        foreach (var method in rules)
        {
            NullArguments.AssertThrowsArgumentNull(method, null, localization);
            NullArguments.AssertThrowsArgumentNull(method, rule, null);
        }
    }
}
