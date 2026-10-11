using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Application.Commands.ProvisionPortalAccount;

public sealed class ProvisionPortalAccountCommandHandlerTests
{
    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly ICustomersModuleApi _customers = Substitute.For<ICustomersModuleApi>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly List<PortalAccount> _added = [];

    public ProvisionPortalAccountCommandHandlerTests()
    {
        _hasher.Hash("portal-pass-123").Returns("hash-portal");
        _accounts.When(r => r.Add(Arg.Any<PortalAccount>())).Do(call => _added.Add(call.Arg<PortalAccount>()));
        _accounts.Finds(null);
        _accounts.Lists();
        CustomerIs(CustomerFixtures.Summary(contacts: CustomerFixtures.Contact()));
    }

    private void CustomerIs(CustomerSummary summary) =>
        _customers.GetCustomerSummaryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success(summary)));

    private Task<Result<PortalAccountDto>> Run(Guid? contactId = null) =>
        new ProvisionPortalAccountCommandHandler(_accounts, _customers, _hasher, Clock.Fixed(), _unitOfWork)
            .Handle(new ProvisionPortalAccountCommand(Ids.Customer, contactId ?? Ids.Contact, "portal-pass-123"), Stubs.Ct);

    [Theory]
    [InlineData(ErrorType.NotFound, "Customers.CustomerNotFound")]
    [InlineData(ErrorType.Forbidden, "Organisation.OutOfScope")]
    public async Task ACustomerLookupFailureIsReturnedAsIsWithNoFurtherCall(ErrorType type, string code)
    {
        _customers.GetCustomerSummaryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<CustomerSummary>(new Error(code, "x", type))));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, code, type);
        await _accounts.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Specification<PortalAccount>>(), Arg.Any<CancellationToken>());
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task AContactNotOnTheCustomerIsNotFoundNamingTheContact()
    {
        Result<PortalAccountDto> result = await Run(Ids.OtherContact);

        Expect.Failure(result, "Identity.PortalContactNotFound", ErrorType.NotFound);
        Assert.Contains(Ids.OtherContact.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInactiveCustomerIsADomainFailure()
    {
        CustomerIs(CustomerFixtures.Summary(isActive: false, contacts: CustomerFixtures.Contact()));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalCustomerInactive", ErrorType.Domain);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheContactLookupComesBeforeTheInactiveCheck()
    {
        CustomerIs(CustomerFixtures.Summary(isActive: false, contacts: CustomerFixtures.Contact()));

        Result<PortalAccountDto> result = await Run(Ids.OtherContact);

        Expect.Failure(result, "Identity.PortalContactNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task TheInactiveCheckComesBeforeThePortalAccessCheck()
    {
        CustomerIs(CustomerFixtures.Summary(isActive: false, contacts: CustomerFixtures.Contact(hasPortalAccess: false)));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalCustomerInactive", ErrorType.Domain);
    }

    [Fact]
    public async Task AContactWithoutPortalAccessIsADomainFailure()
    {
        CustomerIs(CustomerFixtures.Summary(contacts: CustomerFixtures.Contact(hasPortalAccess: false)));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalAccessNotEnabled", ErrorType.Domain);
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task AContactWithoutAnEmailIsADomainFailure()
    {
        CustomerIs(CustomerFixtures.Summary(contacts: CustomerFixtures.Contact(email: null)));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalAccessNotEnabled", ErrorType.Domain);
    }

    [Fact]
    public async Task ANewAccountIsCreatedFromTheSummaryAndTheContact()
    {
        CustomerIs(CustomerFixtures.Summary(language: "ar", tenant: Ids.TenantB, contacts: CustomerFixtures.Contact(name: "  Layla Saleh ", email: " Customer@Example.COM ")));

        Result<PortalAccountDto> result = await Run();

        Assert.True(result.IsSuccess);
        PortalAccount account = Assert.Single(_added);
        Assert.Equal(Ids.TenantB, account.TenantId);
        Assert.Equal(Ids.Customer, account.CustomerId);
        Assert.Equal(Ids.Contact, account.ContactId);
        Assert.Equal("customer@example.com", account.Email.Value);
        Assert.Equal("Layla Saleh", account.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, account.PreferredLocale);
        Assert.Equal("hash-portal", account.PasswordHash);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANewAccountReturnsTheDto()
    {
        Result<PortalAccountDto> result = await Run();

        PortalAccount account = Assert.Single(_added);
        Assert.Equal(new PortalAccountDto(account.Id.Value, Ids.Customer, Ids.Contact, "customer@example.com", null), result.Value);
    }

    [Fact]
    public async Task AnUnparseableContactEmailIsPortalAccessNotEnabled()
    {
        CustomerIs(CustomerFixtures.Summary(contacts: CustomerFixtures.Contact(email: "bad")));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalAccessNotEnabled", ErrorType.Domain);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task AnotherAccountWithTheSameEmailIsAConflictWithNoAddAndNoSave()
    {
        _accounts.Lists(Accounts.New(contactId: Ids.OtherContact));

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalEmailInUse", ErrorType.Conflict);
        Assert.Empty(_added);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExistingAccountForTheContactHasItsPasswordSetByTheAgentWithNoNewAccount()
    {
        PortalAccount existing = Accounts.Failed(5);
        _accounts.Finds(existing);

        Result<PortalAccountDto> result = await Run();

        Assert.True(result.IsSuccess);
        Assert.Empty(_added);
        Assert.Equal("hash-portal", existing.PasswordHash);
        Assert.False(existing.IsLockedOut(Clock.Now));
        Assert.Null(Assert.IsType<PortalPasswordChanged>(Assert.Single(existing.DomainEvents)).KeepSessionId);
        Assert.Equal(existing.Id.Value, result.Value.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExistingAccountOfAnotherTenantIsNotFoundAndNeverTouched()
    {
        PortalAccount existing = Accounts.New(Ids.TenantB);
        _accounts.Finds(existing);

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalContactNotFound", ErrorType.NotFound);
        Assert.Equal("hash-1", existing.PasswordHash);
        Assert.Empty(existing.DomainEvents);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExistingAccountOfAnotherCustomerIsNotFoundAndNeverTouched()
    {
        PortalAccount existing = Accounts.New(customerId: Ids.OtherCustomer);
        _accounts.Finds(existing);

        Result<PortalAccountDto> result = await Run();

        Expect.Failure(result, "Identity.PortalContactNotFound", ErrorType.NotFound);
        Assert.Empty(existing.DomainEvents);
    }

    [Fact]
    public async Task AnUnsupportedCustomerLanguageIsADomainException()
    {
        CustomerIs(CustomerFixtures.Summary(language: "fr", contacts: CustomerFixtures.Contact()));

        await Expect.DomainCode(() => Run(), "Identity.LocaleUnsupported");
    }

    [Fact]
    public async Task ABlankContactNameIsADomainException()
    {
        CustomerIs(CustomerFixtures.Summary(contacts: CustomerFixtures.Contact(name: "  ")));

        await Expect.DomainCode(() => Run(), "Identity.DisplayNameRequired");
    }

    [Fact]
    public async Task TheExistingAccountLookupUsesTheContactSpecification()
    {
        await Run();

        await _accounts.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<PortalAccount>>(s => s is PortalAccountByContactSpecification), Arg.Any<CancellationToken>());
    }
}
