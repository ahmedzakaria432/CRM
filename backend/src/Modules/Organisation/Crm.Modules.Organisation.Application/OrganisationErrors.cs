using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Application;

/// <summary>Expected business failures of the Organisation module's branding use cases (Story 58).</summary>
public static class OrganisationErrors
{
    public static Error OrganisationNotFound() =>
        Error.NotFound("Organisation.OrganisationNotFound", "The organisation was not found.");

    public static Error BrandLogoNotFound(Guid id) =>
        Error.NotFound("Organisation.BrandLogoNotFound", $"Logo '{id}' was not found.");

    public static Error NoPreviousBranding() =>
        Error.Domain("Organisation.NoPreviousBranding", "There is no previous branding to revert to.");

    public static Error LogoTypeNotAllowed() =>
        Error.Domain("Organisation.LogoTypeNotAllowed", "The logo must be a PNG or SVG image.");

    public static Error ContrastTooLow(string detail) =>
        Error.Domain("Organisation.BrandingContrastTooLow", detail);
}
