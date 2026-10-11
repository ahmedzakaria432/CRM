using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.RevertBranding;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RevertBranding;

public sealed class RevertBrandingCommandHandlerTests
{
    private readonly IRepository<OrganisationAggregate, TenantId> _organisations = Substitute.For<IRepository<OrganisationAggregate, TenantId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly OrganisationAggregate _organisation = Organisations.New(tenant: Ids.TenantA);

    private RevertBrandingCommandHandler Handler() => new(_organisations, _unitOfWork, CurrentUsers.Admin());

    [Fact]
    public async Task SuccessSwapsTheBrandingAndReturnsTheSettings()
    {
        _organisations.Holds<OrganisationAggregate, TenantId>(_organisation);
        _organisation.ChangeBranding(Brandings.Valid());

        Result<BrandingSettingsDto> result = await Handler().Handle(new RevertBrandingCommand(Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsCustom);
        Assert.NotNull(result.Value.Previous);
        Assert.True(result.Value.CanRevert);
        Assert.Null(_organisation.Branding);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASecondRevertReappliesTheBranding()
    {
        _organisations.Holds<OrganisationAggregate, TenantId>(_organisation);
        _organisation.ChangeBranding(Brandings.Valid());
        await Handler().Handle(new RevertBrandingCommand(Ids.RowVersion()), Stubs.Ct);

        Result<BrandingSettingsDto> result = await Handler().Handle(new RevertBrandingCommand(Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.Value.IsCustom);
        Assert.Null(result.Value.Previous);
    }

    [Fact]
    public async Task AMissingOrganisationIsNotFound()
    {
        Result<BrandingSettingsDto> result = await Handler().Handle(new RevertBrandingCommand(Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.OrganisationNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NothingToRevertIsADomainResultNotAnException()
    {
        _organisations.Holds<OrganisationAggregate, TenantId>(_organisation);

        Result<BrandingSettingsDto> result = await Handler().Handle(new RevertBrandingCommand(Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.NoPreviousBranding", ErrorType.Domain);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeSaving()
    {
        _organisations.Holds<OrganisationAggregate, TenantId>(_organisation);
        _organisation.ChangeBranding(Brandings.Valid());
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(new RevertBrandingCommand(rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _organisations.SetOriginalRowVersion(_organisation, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void TheCommandInvalidatesTheBrandingsCacheTag()
    {
        Assert.Equal(["organisation:brandings"], new RevertBrandingCommand(Ids.RowVersion()).CacheTagsToInvalidate);
    }

    [Fact]
    public void TheCommandRequiresTheAdminBrandingPermission()
    {
        Assert.Equal(CrmPermissions.AdminBranding, Stubs.PermissionOf(typeof(RevertBrandingCommand)));
    }
}
