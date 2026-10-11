using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.PreviewBranding;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application.Queries.PreviewBranding;

public sealed class PreviewBrandingQueryHandlerTests
{
    private static Task<Result<BrandingPreviewDto>> RunAsync(string primary, string accent) =>
        new PreviewBrandingQueryHandler().Handle(new PreviewBrandingQuery(primary, accent), Stubs.Ct);

    [Fact]
    public async Task APassingPaletteReturnsTwoPassingChecksAndTokens()
    {
        Result<BrandingPreviewDto> result = await RunAsync("#0E6655", "#B7791F");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Checks.Count);
        Assert.All(result.Value.Checks, c => Assert.True(c.Passes));
        Assert.Equal("PrimaryText", result.Value.Checks[0].Name);
        Assert.Equal(4.5, result.Value.Checks[0].Required);
        Assert.Equal("AccentOnSurface", result.Value.Checks[1].Name);
        Assert.Equal(3.0, result.Value.Checks[1].Required);
        Assert.Equal("#0E6655", result.Value.Tokens.Light["--crm-primary-600"]);
        Assert.Equal(22, result.Value.Tokens.Light.Count);
        Assert.Equal(22, result.Value.Tokens.Dark.Count);
    }

    [Fact]
    public async Task RatiosAreRoundedToTwoDecimals()
    {
        Result<BrandingPreviewDto> result = await RunAsync("#0E6655", "#B7791F");

        foreach (ContrastCheckDto check in result.Value.Checks)
        {
            Assert.Equal(Math.Round(check.Ratio, 2), check.Ratio);
        }

        Assert.Equal(Math.Round(BrandColour.White.ContrastWith(Colours.Primary), 2), result.Value.Checks[0].Ratio);
    }

    [Fact]
    public async Task AFailingPaletteStillSucceedsWithPassesFalse()
    {
        Result<BrandingPreviewDto> result = await RunAsync("#FFD400", "#B7791F");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Checks[0].Passes);
        Assert.True(result.Value.Checks[1].Passes);
        Assert.NotEmpty(result.Value.Tokens.Light);
    }

    [Fact]
    public async Task SeventySevenGreyFailsAndSeventySixGreyPasses()
    {
        BrandingPreviewDto fail = (await RunAsync("#777777", "#B7791F")).Value;
        BrandingPreviewDto pass = (await RunAsync("#767676", "#B7791F")).Value;

        Assert.Equal(4.48, fail.Checks[0].Ratio);
        Assert.False(fail.Checks[0].Passes);
        Assert.Equal(4.54, pass.Checks[0].Ratio);
        Assert.True(pass.Checks[0].Passes);
    }

    [Fact]
    public async Task AFailingAccentIsReportedOnTheSecondCheck()
    {
        Result<BrandingPreviewDto> result = await RunAsync("#0E6655", "#F5F5F5");

        Assert.True(result.Value.Checks[0].Passes);
        Assert.False(result.Value.Checks[1].Passes);
    }

    [Fact]
    public async Task AnInvalidHexReachingTheHandlerThrows()
    {
        await Expect.DomainCode(() => RunAsync("blue", "#B7791F"), "Organisation.BrandColourInvalid");
        await Expect.DomainCode(() => RunAsync("#0E6655", "#12345"), "Organisation.BrandColourInvalid");
    }

    [Fact]
    public void TheQueryRequiresTheAdminBrandingPermission()
    {
        Assert.Equal(CrmPermissions.AdminBranding, Stubs.PermissionOf(typeof(PreviewBrandingQuery)));
    }
}
