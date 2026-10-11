using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.ChangeBranding;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application.Theming;

/// <summary>BrandingRefusals is internal: its text is covered through ChangeBrandingCommandHandler, the only caller.</summary>
public sealed class BrandingRefusalsViaHandlerTests
{
    private readonly IRepository<OrganisationAggregate, TenantId> _organisations = Substitute.For<IRepository<OrganisationAggregate, TenantId>>();
    private readonly IRepository<BrandLogo, BrandLogoId> _logos = Substitute.For<IRepository<BrandLogo, BrandLogoId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();

    public BrandingRefusalsViaHandlerTests()
    {
        _organisations.Holds<OrganisationAggregate, TenantId>(Organisations.New(tenant: Ids.TenantA));
    }

    private Task<Result<BrandingSettingsDto>> RunAsync(string primary, string accent) =>
        new ChangeBrandingCommandHandler(_organisations, _logos, _unitOfWork, CurrentUsers.Admin(), FakeLocalization.Resources())
            .Handle(new ChangeBrandingCommand("ع", "E", primary, accent, "Tajawal", null, null, Ids.RowVersion()), Stubs.Ct);

    [Fact]
    public async Task TheEnglishPrimaryTextCarriesTheRealRatioAndRequirement()
    {
        using UiCulture english = UiCulture.Use("en");
        double ratio = BrandColour.White.ContrastWith(BrandColour.Create("#FFD400"));
        string expectedRatio = ratio.ToString("0.00", CultureInfo.InvariantCulture);

        Result<BrandingSettingsDto> result = await RunAsync("#FFD400", "#B7791F");

        Assert.Equal(
            $"Text on the primary colour has a contrast of {expectedRatio}:1, but WCAG AA needs at least 4.5:1. Choose a darker colour.",
            result.Error.Description);
    }

    [Fact]
    public async Task TheArabicTextUsesTheArabicTemplateAndCheckName()
    {
        using UiCulture arabic = UiCulture.Use("ar");

        Result<BrandingSettingsDto> result = await RunAsync("#FFFFFF", "#B7791F");

        Assert.StartsWith("النص على اللون الأساسي بنسبة تباين 1.00:1", result.Error.Description, StringComparison.Ordinal);
        Assert.Contains("اختر لوناً أغمق", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NumbersStayLatinAndDotSeparatedUnderAnArabicSaudiCulture()
    {
        using UiCulture arabic = UiCulture.Use("ar-SA");

        Result<BrandingSettingsDto> result = await RunAsync("#FFFFFF", "#B7791F");

        Assert.Contains("1.00:1", result.Error.Description, StringComparison.Ordinal);
        Assert.Contains("4.5:1", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAccentCheckNamesTheAccentInEnglishAndArabic()
    {
        using (UiCulture.Use("en"))
        {
            Result<BrandingSettingsDto> english = await RunAsync("#0E6655", "#F5F5F5");
            Assert.StartsWith("The accent colour on the page background", english.Error.Description, StringComparison.Ordinal);
            Assert.Contains("at least 3.0:1", english.Error.Description, StringComparison.Ordinal);
        }

        using (UiCulture.Use("ar"))
        {
            Result<BrandingSettingsDto> arabic = await RunAsync("#0E6655", "#F5F5F5");
            Assert.StartsWith("لون التمييز على خلفية الصفحة", arabic.Error.Description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ThePrimaryFailureWinsWhenBothFail()
    {
        using UiCulture english = UiCulture.Use("en");

        Result<BrandingSettingsDto> result = await RunAsync("#FFFFFF", "#FFFFFF");

        Assert.StartsWith("Text on the primary colour", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APassingPaletteYieldsNoRefusal()
    {
        Result<BrandingSettingsDto> result = await RunAsync("#0E6655", "#B7791F");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ARatioJustBelowTheLimitIsRefusedAndJustAboveIsAccepted()
    {
        Result<BrandingSettingsDto> below = await RunAsync("#777777", "#B7791F");
        Result<BrandingSettingsDto> above = await RunAsync("#767676", "#B7791F");

        Expect.Failure(below, "Organisation.BrandingContrastTooLow", ErrorType.Domain);
        Assert.True(above.IsSuccess);
    }
}
