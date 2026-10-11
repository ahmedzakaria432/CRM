using Crm.Modules.Organisation.Application.Queries.PreviewBranding;
using Crm.Modules.Organisation.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Organisation.Tests.Application.Queries.PreviewBranding;

public sealed class PreviewBrandingQueryValidatorTests
{
    private readonly PreviewBrandingQueryValidator _validator = new(FakeLocalization.Resources());

    private ValidationResult Validate(string primary, string accent)
    {
        using UiCulture english = UiCulture.Use("en");
        return _validator.Validate(new PreviewBrandingQuery(primary, accent));
    }

    [Fact]
    public void TwoHexColoursPass()
    {
        Expect.Valid(Validate("#0E6655", "#b7791f"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    public void ABadPrimaryFails(string primary)
    {
        Expect.Invalid(Validate(primary, "#B7791F"), nameof(PreviewBrandingQuery.PrimaryColour));
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    public void ABadAccentFails(string accent)
    {
        Expect.Invalid(Validate("#0E6655", accent), nameof(PreviewBrandingQuery.AccentColour));
    }

    [Fact]
    public void TheMessageIsTheLocalisedColourText()
    {
        ValidationResult result = Validate("red", "#B7791F");

        Assert.Equal("A colour must be a hex value like #1A2B3C.", Assert.Single(result.Errors).ErrorMessage);
    }
}
