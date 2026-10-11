using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetBrandingSettings;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetBrandingSettings;

public sealed class GetBrandingSettingsQueryHandlerTests
{
    private readonly IRepository<OrganisationAggregate, TenantId> _repository = Substitute.For<IRepository<OrganisationAggregate, TenantId>>();
    private readonly OrganisationAggregate _organisation = Organisations.New(tenant: Ids.TenantA, name: "Demo Organisation");

    private Task<Result<BrandingSettingsDto>> RunAsync() =>
        new GetBrandingSettingsQueryHandler(_repository, CurrentUsers.Admin()).Handle(new GetBrandingSettingsQuery(), Stubs.Ct);

    [Fact]
    public async Task CustomBrandingIsReportedWithThePaletteHexValues()
    {
        _repository.Holds<OrganisationAggregate, TenantId>(_organisation);
        _organisation.ChangeBranding(Brandings.Valid(english: "Custom"));

        Result<BrandingSettingsDto> result = await RunAsync();

        Assert.True(result.Value.IsCustom);
        Assert.Equal("#0E6655", result.Value.Current.PrimaryColour);
        Assert.Equal("#B7791F", result.Value.Current.AccentColour);
        Assert.Equal("Custom", result.Value.Current.DisplayName.En);
        Assert.Equal("Tajawal", result.Value.Current.ArabicFont);
        Assert.True(result.Value.CanRevert);
    }

    [Fact]
    public async Task NoBrandingShowsTheProductDefaults()
    {
        _repository.Holds<OrganisationAggregate, TenantId>(_organisation);

        Result<BrandingSettingsDto> result = await RunAsync();

        Assert.False(result.Value.IsCustom);
        Assert.Equal("#145EC1", result.Value.Current.PrimaryColour);
        Assert.Equal("#0F766E", result.Value.Current.AccentColour);
        Assert.Equal("IbmPlexSansArabic", result.Value.Current.ArabicFont);
        Assert.Equal("Demo Organisation", result.Value.Current.DisplayName.En);
        Assert.Equal("Demo Organisation", result.Value.OrganisationName.En);
        Assert.False(result.Value.CanRevert);
        Assert.Null(result.Value.Previous);
    }

    [Fact]
    public async Task AMissingOrganisationIsNotFound()
    {
        Result<BrandingSettingsDto> result = await RunAsync();

        Expect.Failure(result, "Organisation.OrganisationNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task TheOrganisationIsReadByTheCallersTenant()
    {
        await RunAsync();

        await _repository.Received(1).GetByIdAsync(Ids.TenantA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRowVersionIsCopiedToTheDto()
    {
        _repository.Holds<OrganisationAggregate, TenantId>(_organisation);

        Result<BrandingSettingsDto> result = await RunAsync();

        Assert.Same(_organisation.RowVersion, result.Value.RowVersion);
    }

    [Fact]
    public void TheQueryRequiresTheAdminBrandingPermissionAndIsNotCached()
    {
        Assert.Equal(CrmPermissions.AdminBranding, Stubs.PermissionOf(typeof(GetBrandingSettingsQuery)));
        Assert.False(typeof(ICacheableQuery).IsAssignableFrom(typeof(GetBrandingSettingsQuery)));
    }
}
