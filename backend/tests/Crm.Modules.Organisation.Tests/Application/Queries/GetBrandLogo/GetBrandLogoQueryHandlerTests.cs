using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetBrandLogo;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetBrandLogo;

public sealed class GetBrandLogoQueryHandlerTests
{
    private readonly IRepository<BrandLogo, BrandLogoId> _repository = Substitute.For<IRepository<BrandLogo, BrandLogoId>>();

    [Fact]
    public async Task AFoundLogoReturnsItsBytesAndMediaType()
    {
        BrandLogo logo = Logos.Svg();
        _repository.Holds<BrandLogo, BrandLogoId>(logo);

        Result<BrandLogoContentDto> result = await new GetBrandLogoQueryHandler(_repository).Handle(new GetBrandLogoQuery(logo.Id.Value), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(logo.Content, result.Value.Content);
        Assert.Equal("image/svg+xml", result.Value.ContentType);
    }

    [Fact]
    public async Task APngReturnsThePngMediaType()
    {
        BrandLogo logo = Logos.Png();
        _repository.Holds<BrandLogo, BrandLogoId>(logo);

        Result<BrandLogoContentDto> result = await new GetBrandLogoQueryHandler(_repository).Handle(new GetBrandLogoQuery(logo.Id.Value), Stubs.Ct);

        Assert.Equal("image/png", result.Value.ContentType);
    }

    [Fact]
    public async Task AMissingLogoIsNotFoundNamingTheId()
    {
        var id = Guid.NewGuid();

        Result<BrandLogoContentDto> result = await new GetBrandLogoQueryHandler(_repository).Handle(new GetBrandLogoQuery(id), Stubs.Ct);

        Expect.Failure(result, "Organisation.BrandLogoNotFound", ErrorType.NotFound);
        Assert.Contains(id.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void TheQueryIsAnonymousAndNeverCachedSoBytesStayOutOfRedis()
    {
        Assert.True(Stubs.HasAttribute<AllowAnonymousRequestAttribute>(typeof(GetBrandLogoQuery)));
        Assert.False(typeof(ICacheableQuery).IsAssignableFrom(typeof(GetBrandLogoQuery)));
    }
}
