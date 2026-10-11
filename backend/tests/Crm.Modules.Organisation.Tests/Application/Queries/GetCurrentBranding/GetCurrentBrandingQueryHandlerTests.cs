using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetCurrentBranding;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetCurrentBranding;

public sealed class GetCurrentBrandingQueryHandlerTests
{
    private readonly IRepository<OrganisationAggregate, TenantId> _repository = Substitute.For<IRepository<OrganisationAggregate, TenantId>>();
    private readonly OrganisationAggregate _organisation = Organisations.New(tenant: Ids.TenantA, name: "Demo Organisation");

    private Task<Result<PublicBrandingDto>> RunAsync() =>
        new GetCurrentBrandingQueryHandler(_repository, CurrentUsers.Admin()).Handle(new GetCurrentBrandingQuery(), Stubs.Ct);

    [Fact]
    public async Task BrandedOrganisationsGetGeneratedTokens()
    {
        _repository.Holds<OrganisationAggregate, TenantId>(_organisation);
        BrandLogoId light = BrandLogoId.New();
        _organisation.ChangeBranding(Brandings.Valid(light, english: "Custom"));

        Result<PublicBrandingDto> result = await RunAsync();

        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("#0E6655", result.Value.Tokens.Light["--crm-primary-600"]);
        Assert.Equal("#B7791F", result.Value.Tokens.Light["--crm-accent-600"]);
        Assert.Equal("Custom", result.Value.DisplayName.En);
        Assert.Equal("Tajawal", result.Value.ArabicFont);
        Assert.Equal(light.Value, result.Value.LightLogoId);
        Assert.Null(result.Value.DarkLogoId);
    }

    [Fact]
    public async Task UnbrandedOrganisationsGetNoTokens()
    {
        _repository.Holds<OrganisationAggregate, TenantId>(_organisation);

        Result<PublicBrandingDto> result = await RunAsync();

        Assert.Null(result.Value.Tokens);
        Assert.Equal("Demo Organisation", result.Value.DisplayName.En);
        Assert.Equal("IbmPlexSansArabic", result.Value.ArabicFont);
        Assert.Null(result.Value.LightLogoId);
    }

    [Fact]
    public async Task AMissingOrganisationIsNotFound()
    {
        Result<PublicBrandingDto> result = await RunAsync();

        Expect.Failure(result, "Organisation.OrganisationNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task TheOrganisationIsReadByTheCallersTenant()
    {
        await RunAsync();

        await _repository.Received(1).GetByIdAsync(Ids.TenantA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheQueryIsCachedUnderTheCurrentBrandingKeyAndTheBrandingsTag()
    {
        var query = new GetCurrentBrandingQuery();

        Assert.Equal("organisation:brandings:current", query.CacheKey);
        Assert.Equal(["organisation:brandings"], query.CacheTags);
        Assert.Null(((Crm.BuildingBlocks.Application.Caching.ICacheableQuery)query).CacheDuration);
    }

    [Fact]
    public void TheQueryRequiresAnAuthenticatedUser()
    {
        Assert.True(Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(typeof(GetCurrentBrandingQuery)));
    }
}
