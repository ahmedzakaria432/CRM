using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.ChangeBranding;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application.Commands.ChangeBranding;

public sealed class ChangeBrandingCommandHandlerTests
{
    private readonly IRepository<OrganisationAggregate, TenantId> _organisations = Substitute.For<IRepository<OrganisationAggregate, TenantId>>();
    private readonly IRepository<BrandLogo, BrandLogoId> _logos = Substitute.For<IRepository<BrandLogo, BrandLogoId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly OrganisationAggregate _organisation = Organisations.New(tenant: Ids.TenantA, name: "Demo");

    private ChangeBrandingCommandHandler Handler(ILocalizationService? localization = null) =>
        new(_organisations, _logos, _unitOfWork, CurrentUsers.Admin(), localization ?? FakeLocalization.Resources());

    private static ChangeBrandingCommand Command(
        string primary = "#0E6655",
        string accent = "#B7791F",
        string font = "Tajawal",
        Guid? light = null,
        Guid? dark = null,
        string english = "Brand",
        byte[]? rowVersion = null) =>
        new("علامة", english, primary, accent, font, light, dark, rowVersion ?? Ids.RowVersion());

    private void OrganisationExists() => _organisations.Holds<OrganisationAggregate, TenantId>(_organisation);

    [Fact]
    public async Task SuccessSetsTheBrandingAndReturnsTheSettings()
    {
        OrganisationExists();

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCustom);
        Assert.True(result.Value.CanRevert);
        Assert.Null(result.Value.Previous);
        Assert.Equal("#0E6655", result.Value.Current.PrimaryColour);
        Assert.Equal("#B7791F", result.Value.Current.AccentColour);
        Assert.Equal("Tajawal", result.Value.Current.ArabicFont);
        Assert.Equal("Brand", result.Value.Current.DisplayName.En);
        Assert.NotNull(_organisation.Branding);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ALightLogoIsResolvedThroughTheLogoRepository()
    {
        OrganisationExists();
        BrandLogo logo = Logos.Svg(Ids.TenantA);
        _logos.Holds<BrandLogo, BrandLogoId>(logo);

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(light: logo.Id.Value), Stubs.Ct);

        Assert.Equal(logo.Id.Value, result.Value.Current.LightLogoId);
        Assert.Null(result.Value.Current.DarkLogoId);
    }

    [Fact]
    public async Task LightAndDarkLogosAreBothResolved()
    {
        OrganisationExists();
        BrandLogo light = Logos.Svg(Ids.TenantA);
        BrandLogo dark = Logos.Png(Ids.TenantA);
        _logos.Holds<BrandLogo, BrandLogoId>(light);
        _logos.Holds<BrandLogo, BrandLogoId>(dark);

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(light: light.Id.Value, dark: dark.Id.Value), Stubs.Ct);

        Assert.Equal(light.Id.Value, result.Value.Current.LightLogoId);
        Assert.Equal(dark.Id.Value, result.Value.Current.DarkLogoId);
    }

    [Fact]
    public async Task AMissingOrganisationIsNotFound()
    {
        Result<BrandingSettingsDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Organisation.OrganisationNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task APrimaryColourWithTooLittleContrastIsADomainResultWithTheEnglishDetail()
    {
        OrganisationExists();
        using UiCulture english = UiCulture.Use("en");

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(primary: "#FFFFFF"), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandingContrastTooLow", ErrorType.Domain);
        Assert.Equal(
            "Text on the primary colour has a contrast of 1.00:1, but WCAG AA needs at least 4.5:1. Choose a darker colour.",
            result.Error.Description);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Null(_organisation.Branding);
    }

    [Fact]
    public async Task TheDetailIsLocalisedToArabicWithLatinDigits()
    {
        OrganisationExists();
        using UiCulture arabic = UiCulture.Use("ar");

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(primary: "#FFFFFF"), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandingContrastTooLow", ErrorType.Domain);
        Assert.Contains("النص على اللون الأساسي", result.Error.Description, StringComparison.Ordinal);
        Assert.Contains("1.00:1", result.Error.Description, StringComparison.Ordinal);
        Assert.Contains("4.5:1", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAccentColourWithTooLittleContrastNamesTheAccentCheck()
    {
        OrganisationExists();
        using UiCulture english = UiCulture.Use("en");

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(accent: "#F5F5F5"), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandingContrastTooLow", ErrorType.Domain);
        Assert.StartsWith("The accent colour on the page background has a contrast of", result.Error.Description, StringComparison.Ordinal);
        Assert.Contains("at least 3.0:1", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APassingPaletteProducesNoRefusalFromTheKeyedLocalisation()
    {
        OrganisationExists();

        Result<BrandingSettingsDto> result = await Handler(FakeLocalization.Keys()).Handle(Command(), Stubs.Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AFailingPaletteUsesTheLocalisationKeyForTheTemplate()
    {
        OrganisationExists();
        ILocalizationService localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>() == "Organisation.Refusal.ContrastTooLow" ? "{0}|{1}|{2}" : "CHECK");

        Result<BrandingSettingsDto> result = await Handler(localization).Handle(Command(primary: "#FFFFFF"), Stubs.Ct);

        Assert.Equal("CHECK|1.00|4.5", result.Error.Description);
    }

    [Fact]
    public async Task AMissingLogoIsNotFound()
    {
        OrganisationExists();
        Guid missing = Guid.NewGuid();

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(light: missing), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandLogoNotFound", ErrorType.NotFound);
        Assert.Contains(missing.ToString(), result.Error.Description, StringComparison.Ordinal);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ALightLogoOfAnotherTenantIsTheSameNotFound()
    {
        OrganisationExists();
        BrandLogo foreign = Logos.Svg(Ids.TenantB);
        _logos.Holds<BrandLogo, BrandLogoId>(foreign);

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(light: foreign.Id.Value), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandLogoNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADarkLogoOfAnotherTenantIsTheSameNotFound()
    {
        OrganisationExists();
        BrandLogo own = Logos.Svg(Ids.TenantA);
        BrandLogo foreign = Logos.Png(Ids.TenantB);
        _logos.Holds<BrandLogo, BrandLogoId>(own);
        _logos.Holds<BrandLogo, BrandLogoId>(foreign);

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(light: own.Id.Value, dark: foreign.Id.Value), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandLogoNotFound", ErrorType.NotFound);
        Assert.Contains(foreign.Id.Value.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADarkLogoWithoutALightLogoIsRefusedByTheAggregate()
    {
        OrganisationExists();
        BrandLogo dark = Logos.Png(Ids.TenantA);
        _logos.Holds<BrandLogo, BrandLogoId>(dark);

        await Expect.DomainCode(() => Handler().Handle(Command(dark: dark.Id.Value), Stubs.Ct), "Organisation.DarkLogoWithoutLight");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Arial")]
    [InlineData("tajawal")]
    public async Task AnUnsupportedFontStringThrowsBecauseTheValidatorIsTheGuard(string font)
    {
        OrganisationExists();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Handler().Handle(Command(font: font), Stubs.Ct));
    }

    [Fact]
    public async Task ANumericFontStringReachingTheHandlerIsParsedAsTheEnumValue()
    {
        // Pinned (Follow-up): Enum.Parse accepts "1" as NotoKufiArabic; only the validator rejects numbers.
        OrganisationExists();

        Result<BrandingSettingsDto> result = await Handler().Handle(Command(font: "1"), Stubs.Ct);

        Assert.Equal("NotoKufiArabic", result.Value.Current.ArabicFont);
    }

    [Fact]
    public async Task AnUndefinedNumericFontIsRefusedByTheAggregate()
    {
        OrganisationExists();

        await Expect.DomainCode(() => Handler().Handle(Command(font: "99"), Stubs.Ct), "Organisation.ArabicFontUnsupported");
    }

    [Fact]
    public async Task AnInvalidHexReachingTheHandlerThrows()
    {
        OrganisationExists();

        await Expect.DomainCode(() => Handler().Handle(Command(primary: "blue"), Stubs.Ct), "Organisation.BrandColourInvalid");
        await Expect.DomainCode(() => Handler().Handle(Command(accent: "#12345"), Stubs.Ct), "Organisation.BrandColourInvalid");
    }

    [Fact]
    public async Task SavingADifferentBrandingKeepsTheFirstAsPrevious()
    {
        OrganisationExists();
        await Handler().Handle(Command(english: "First"), Stubs.Ct);

        Result<BrandingSettingsDto> second = await Handler().Handle(Command(english: "Second"), Stubs.Ct);

        Assert.Equal("Second", second.Value.Current.DisplayName.En);
        Assert.NotNull(second.Value.Previous);
        Assert.Equal("First", second.Value.Previous.DisplayName.En);
    }

    [Fact]
    public async Task SavingTheSameBrandingTwiceKeepsThePreviousAsTheDefaults()
    {
        OrganisationExists();
        await Handler().Handle(Command(), Stubs.Ct);

        Result<BrandingSettingsDto> second = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Null(second.Value.Previous);
        Assert.True(second.Value.CanRevert);
    }

    [Fact]
    public async Task TheRowVersionIsSetOnTheOrganisationBeforeSaving()
    {
        OrganisationExists();
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(Command(rowVersion: rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _organisations.SetOriginalRowVersion(_organisation, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeTheBrandingIsValidated()
    {
        // Pinned: harmless ordering (the version only matters at save), but the refusal happens after it.
        OrganisationExists();

        await Handler().Handle(Command(primary: "#FFFFFF"), Stubs.Ct);

        _organisations.Received(1).SetOriginalRowVersion(_organisation, Arg.Any<byte[]>());
    }

    [Fact]
    public void TheCommandInvalidatesTheBrandingsCacheTag()
    {
        Assert.Equal(["organisation:brandings"], Command().CacheTagsToInvalidate);

    }

    [Fact]
    public void TheCommandRequiresTheAdminBrandingPermission()
    {
        Assert.Equal(CrmPermissions.AdminBranding, Stubs.PermissionOf(typeof(ChangeBrandingCommand)));
    }
}
