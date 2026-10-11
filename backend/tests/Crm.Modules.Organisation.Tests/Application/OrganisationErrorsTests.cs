using Crm.Modules.Organisation.Application;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application;

public sealed class OrganisationErrorsTests
{
    [Fact]
    public void OrganisationNotFoundIsNotFound()
    {
        Error error = OrganisationErrors.OrganisationNotFound();

        Assert.Equal("Organisation.OrganisationNotFound", error.Code);
        Assert.Equal("The organisation was not found.", error.Description);
        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    [Fact]
    public void BrandLogoNotFoundNamesTheId()
    {
        var id = Guid.NewGuid();

        Error error = OrganisationErrors.BrandLogoNotFound(id);

        Assert.Equal("Organisation.BrandLogoNotFound", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Contains(id.ToString(), error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void NoPreviousBrandingIsADomainError()
    {
        Error error = OrganisationErrors.NoPreviousBranding();

        Assert.Equal("Organisation.NoPreviousBranding", error.Code);
        Assert.Equal("There is no previous branding to revert to.", error.Description);
        Assert.Equal(ErrorType.Domain, error.Type);
    }

    [Fact]
    public void LogoTypeNotAllowedIsADomainError()
    {
        Error error = OrganisationErrors.LogoTypeNotAllowed();

        Assert.Equal("Organisation.LogoTypeNotAllowed", error.Code);
        Assert.Equal("The logo must be a PNG or SVG image.", error.Description);
        Assert.Equal(ErrorType.Domain, error.Type);
    }

    [Fact]
    public void ContrastTooLowCarriesTheSuppliedDetail()
    {
        Error error = OrganisationErrors.ContrastTooLow("detail text");

        Assert.Equal("Organisation.BrandingContrastTooLow", error.Code);
        Assert.Equal("detail text", error.Description);
        Assert.Equal(ErrorType.Domain, error.Type);
    }
}
