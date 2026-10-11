using System.Text;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.UploadBrandLogo;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.UploadBrandLogo;

public sealed class UploadBrandLogoCommandHandlerTests
{
    private readonly IRepository<BrandLogo, BrandLogoId> _repository = Substitute.For<IRepository<BrandLogo, BrandLogoId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly ISvgSanitizer _sanitizer = Substitute.For<ISvgSanitizer>();
    private BrandLogo? _added;

    public UploadBrandLogoCommandHandlerTests()
    {
        _repository.When(r => r.Add(Arg.Any<BrandLogo>())).Do(call => _added = call.Arg<BrandLogo>());
    }

    private UploadBrandLogoCommandHandler Handler(ICurrentUserService? user = null) =>
        new(_repository, _unitOfWork, user ?? CurrentUsers.Admin(), _sanitizer);

    [Fact]
    public async Task APngIsSavedWithoutCallingTheSanitizer()
    {
        byte[] png = Logos.PngBytes(32);

        Result<BrandLogoDto> result = await Handler().Handle(new UploadBrandLogoCommand(png), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Equal(32, result.Value.SizeBytes);
        Assert.Equal(_added?.Id.Value, result.Value.Id);
        _sanitizer.DidNotReceive().Sanitize(Arg.Any<byte[]>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnSvgIsSanitisedAndTheSanitisedBytesAreStored()
    {
        byte[] original = Logos.SvgBytes();
        byte[] clean = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        _sanitizer.Sanitize(Arg.Any<byte[]>()).Returns(clean);

        Result<BrandLogoDto> result = await Handler().Handle(new UploadBrandLogoCommand(original), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("image/svg+xml", result.Value.ContentType);
        Assert.Equal(clean.Length, result.Value.SizeBytes);
        Assert.Equal(clean, _added?.Content);
        _sanitizer.Received(1).Sanitize(original);
    }

    [Fact]
    public async Task AnSvgTheSanitizerRefusesIsADomainResultAndNothingIsStored()
    {
        _sanitizer.Sanitize(Arg.Any<byte[]>()).Returns((byte[]?)null);

        Result<BrandLogoDto> result = await Handler().Handle(new UploadBrandLogoCommand(Logos.SvgBytes()), Stubs.Ct);

        Expect.Failure(result, "Organisation.LogoTypeNotAllowed", ErrorType.Domain);
        _repository.DidNotReceive().Add(Arg.Any<BrandLogo>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyContentThrows()
    {
        await Expect.DomainCode(() => Handler().Handle(new UploadBrandLogoCommand([]), Stubs.Ct), "Organisation.LogoEmpty");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContentOverFiveHundredAndTwelveKilobytesThrows()
    {
        byte[] large = Logos.PngBytes(BrandLogo.MaxBytes + 1);

        await Expect.DomainCode(() => Handler().Handle(new UploadBrandLogoCommand(large), Stubs.Ct), "Organisation.LogoTooLarge");
    }

    [Fact]
    public async Task AnUnknownTypeThrowsAndTheSanitizerIsNeverCalled()
    {
        byte[] gif = Encoding.ASCII.GetBytes("GIF89a......");

        await Expect.DomainCode(() => Handler().Handle(new UploadBrandLogoCommand(gif), Stubs.Ct), "Organisation.LogoTypeNotAllowed");
        _sanitizer.DidNotReceive().Sanitize(Arg.Any<byte[]>());
    }

    [Fact]
    public async Task TheLogoIsStampedWithTheCallersTenant()
    {
        await Handler(CurrentUsers.Admin(Ids.TenantB)).Handle(new UploadBrandLogoCommand(Logos.PngBytes()), Stubs.Ct);

        Assert.Equal(Ids.TenantB, _added?.TenantId);
    }

    [Fact]
    public async Task ANoneTenantScopeThrowsTenantRequired()
    {
        ICurrentUserService noTenant = CurrentUsers.Agent(OrganisationScope.None);

        await Expect.DomainCode(() => Handler(noTenant).Handle(new UploadBrandLogoCommand(Logos.PngBytes()), Stubs.Ct), "Organisation.TenantRequired");
        _repository.DidNotReceive().Add(Arg.Any<BrandLogo>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminBrandingPermission()
    {
        Assert.Equal(CrmPermissions.AdminBranding, Stubs.PermissionOf(typeof(UploadBrandLogoCommand)));
    }
}
