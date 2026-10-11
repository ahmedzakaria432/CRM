using Crm.Modules.Organisation.Application.Localization;
using Crm.Modules.Organisation.Application.Queries.GetPublicBranding;
using Crm.Modules.Organisation.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetPublicBranding;

public sealed class GetPublicBrandingQueryValidatorTests
{
    private readonly GetPublicBrandingQueryValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(string code) => _validator.Validate(new GetPublicBrandingQuery(code));

    [Theory]
    [InlineData("demo")]
    [InlineData("DEMO-1")]
    [InlineData("ab")]
    public void ValidCodesPass(string code)
    {
        Expect.Valid(Validate(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a b")]
    public void InvalidCodesFail(string code)
    {
        Expect.Invalid(Validate(code), nameof(GetPublicBrandingQuery.Organisation));
    }

    [Fact]
    public void ATwentyOneCharacterCodeFails()
    {
        Expect.Invalid(Validate(new string('a', 21)), nameof(GetPublicBrandingQuery.Organisation));
    }

    [Fact]
    public void TheMessageIsTheOrganisationCodeInvalidKey()
    {
        ValidationResult result = Validate("a b");

        Assert.Equal(OrganisationMessageKeys.Validation.OrganisationCodeInvalid, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void TheRealEnglishMessageIsLocalised()
    {
        using UiCulture english = UiCulture.Use("en");
        var validator = new GetPublicBrandingQueryValidator(FakeLocalization.Resources());

        ValidationResult result = validator.Validate(new GetPublicBrandingQuery("a b"));

        Assert.Equal("The organisation code must be 2-20 characters of A-Z, 0-9, or '-'.", Assert.Single(result.Errors).ErrorMessage);
    }
}
