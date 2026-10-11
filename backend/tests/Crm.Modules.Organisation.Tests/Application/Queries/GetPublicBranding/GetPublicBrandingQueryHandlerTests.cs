using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetPublicBranding;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetPublicBranding;

/// <summary>Runs against EF InMemory through the real repository: the organisation is not scoped, so an anonymous caller reads it.</summary>
public sealed class GetPublicBrandingQueryHandlerTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task SeedAsync(bool active = true, bool branded = true)
    {
        await using OrganisationDbContext store = OrganisationTestContext.Store(_database);
        OrganisationAggregate organisation = Organisations.New("RYD-01", "Riyadh Org", Ids.TenantA);
        if (branded)
        {
            organisation.ChangeBranding(Brandings.Valid(english: "Brand"));
        }

        store.Organisations.Add(organisation);
        store.Organisations.Add(Organisations.New("OTHER", "Other Org", Ids.TenantB));
        await store.SaveChangesAsync(Stubs.Ct);

        if (!active)
        {
            store.Entry(organisation).Property(o => o.IsActive).CurrentValue = false;
            await store.SaveChangesAsync(Stubs.Ct);
        }
    }

    private async Task<Result<PublicBrandingDto>> RunAsync(string code)
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Anonymous());
        var handler = new GetPublicBrandingQueryHandler(OrganisationTestContext.Repo<OrganisationAggregate, TenantId>(context));
        return await handler.Handle(new GetPublicBrandingQuery(code), Stubs.Ct);
    }

    [Fact]
    public async Task ABrandedOrganisationIsFoundByCode()
    {
        await SeedAsync();

        Result<PublicBrandingDto> result = await RunAsync("RYD-01");

        Assert.True(result.IsSuccess);
        Assert.Equal("Brand", result.Value.DisplayName.En);
        Assert.NotNull(result.Value.Tokens);
    }

    [Fact]
    public async Task AnUnbrandedOrganisationShowsItsNameWithoutTokens()
    {
        await SeedAsync(branded: false);

        Result<PublicBrandingDto> result = await RunAsync("RYD-01");

        Assert.Equal("Riyadh Org", result.Value.DisplayName.En);
        Assert.Null(result.Value.Tokens);
    }

    [Fact]
    public async Task TheCodeIsNormalisedBeforeTheLookup()
    {
        await SeedAsync();

        Result<PublicBrandingDto> result = await RunAsync(" ryd-01 ");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AnUnknownCodeIsNotFound()
    {
        await SeedAsync();

        Result<PublicBrandingDto> result = await RunAsync("NOPE");

        Expect.Failure(result, "Organisation.OrganisationNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task AnInactiveOrganisationIsNotFound()
    {
        await SeedAsync(active: false);

        Result<PublicBrandingDto> result = await RunAsync("RYD-01");

        Expect.Failure(result, "Organisation.OrganisationNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task AnInvalidCodeReachingTheHandlerThrows()
    {
        await SeedAsync();

        await Expect.DomainCode(() => RunAsync("a b"), "Organisation.CodeInvalid");
    }

    [Fact]
    public void TheQueryIsCachedPerNormalisedCodeUnderTheBrandingsTag()
    {
        var query = new GetPublicBrandingQuery(" ryd-01 ");

        Assert.Equal("organisation:brandings:code:RYD-01", query.CacheKey);
        Assert.Equal(["organisation:brandings"], query.CacheTags);
    }

    [Fact]
    public void TheQueryIsAnonymous()
    {
        Assert.True(Stubs.HasAttribute<AllowAnonymousRequestAttribute>(typeof(GetPublicBrandingQuery)));
    }
}
