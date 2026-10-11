using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Tests.Application.Commands.UpdatePortalProfile;

public sealed class UpdatePortalProfileCommandHandlerTests
{
    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task<Result<PortalMeDto>> Run(ICurrentUserService caller, string name = "New Name", string locale = "ar") =>
        new UpdatePortalProfileCommandHandler(_accounts, caller, _unitOfWork)
            .Handle(new UpdatePortalProfileCommand(name, locale), Stubs.Ct);

    private static ICurrentUserService Portal(bool isAdmin = true) => CurrentUsers.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, isAdmin);

    [Fact]
    public async Task ANonGuidUserIsPortalOnlyWithNoRepositoryCall()
    {
        Result<PortalMeDto> result = await Run(CurrentUsers.WithIds(Scopes.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, true), "not-a-guid", null));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<PortalAccountId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingAccountIsPortalOnly()
    {
        Result<PortalMeDto> result = await Run(Portal());

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AStaffPrincipalIsPortalOnly()
    {
        PortalAccount account = Accounts.New();
        _accounts.Holds(account);

        Result<PortalMeDto> result = await Run(CurrentUsers.WithIds(Scope.OfTenantA(), Ids.Account.ToString(), null));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        Assert.Equal("Layla Saleh", account.DisplayName.Value);
    }

    [Fact]
    public async Task AGrantContactThatDisagreesIsPortalOnlyWithNoChangeAndNoSave()
    {
        PortalAccount account = Accounts.New();
        _accounts.Holds(account);

        Result<PortalMeDto> result = await Run(CurrentUsers.Portal(Ids.TenantA, Ids.Customer, Ids.OtherContact, true));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        Assert.Equal("Layla Saleh", account.DisplayName.Value);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AGrantCustomerThatDisagreesIsPortalOnly()
    {
        _accounts.Holds(Accounts.New());

        Result<PortalMeDto> result = await Run(CurrentUsers.Portal(Ids.TenantA, Ids.OtherCustomer, Ids.Contact, true));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ATenantThatDisagreesIsPortalOnly()
    {
        _accounts.Holds(Accounts.New());

        Result<PortalMeDto> result = await Run(CurrentUsers.Portal(Ids.TenantB, Ids.Customer, Ids.Contact, true));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SuccessReplacesNameAndLocaleSavesOnceAndReturnsTheNewValues()
    {
        PortalAccount account = Accounts.New();
        _accounts.Holds(account);

        Result<PortalMeDto> result = await Run(Portal(isAdmin: false));

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", account.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, account.PreferredLocale);
        PortalMeDto me = result.Value;
        Assert.Equal(Ids.Account, me.Id);
        Assert.Equal("customer@example.com", me.Email);
        Assert.Equal("New Name", me.DisplayName);
        Assert.Equal("ar", me.PreferredLocale);
        Assert.Equal(Ids.Customer, me.CustomerId);
        Assert.Equal(Ids.Contact, me.ContactId);
        Assert.False(me.IsAccountAdmin);
        Assert.Equal(["portal.account", "portal.tickets"], me.Permissions);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheAccountAdminFlagComesFromTheGrant()
    {
        _accounts.Holds(Accounts.New());

        Result<PortalMeDto> result = await Run(Portal(isAdmin: true));

        Assert.True(result.Value.IsAccountAdmin);
    }

    [Fact]
    public async Task ABlankNameIsADomainException()
    {
        _accounts.Holds(Accounts.New());

        await Expect.DomainCode(() => Run(Portal(), name: " "), "Identity.DisplayNameRequired");
    }

    [Fact]
    public async Task AnUnsupportedLocaleIsADomainException()
    {
        _accounts.Holds(Accounts.New());

        await Expect.DomainCode(() => Run(Portal(), locale: "fr"), "Identity.LocaleUnsupported");
    }
}
