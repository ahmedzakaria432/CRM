using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.PortalSignIn;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Application.Commands.PortalSignIn;

public sealed class PortalSignInCommandHandlerTests
{
    private const string Password = "Correct-horse-9";

    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly IRepository<PortalSession, PortalSessionId> _sessions = Substitute.For<IRepository<PortalSession, PortalSessionId>>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly IAccessTokenIssuer _issuer = Substitute.For<IAccessTokenIssuer>();
    private readonly ICustomersModuleApi _customers = Substitute.For<ICustomersModuleApi>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly RecordingLogger<PortalSignInCommandHandler> _logger = new();
    private readonly List<PortalSession> _added = [];
    private PortalAccessTokenSubject? _subject;

    public PortalSignInCommandHandlerTests()
    {
        _hasher.Verify("hash-1", Password).Returns(true);
        _generator.Generates("refresh-token", "refresh-hash");
        _sessions.When(r => r.Add(Arg.Any<PortalSession>())).Do(call => _added.Add(call.Arg<PortalSession>()));
        _issuer.IssuePortal(Arg.Any<PortalAccessTokenSubject>(), Arg.Any<DateTimeOffset>())
            .Returns(call =>
            {
                _subject = call.Arg<PortalAccessTokenSubject>();
                return new IssuedAccessToken("portal-jwt", Clock.Now.AddMinutes(15));
            });
        ContactIs(CustomerFixtures.Match());
    }

    private void ContactIs(PortalContactMatch match) =>
        _customers.GetPortalContactAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success(match)));

    private Task<Result<SignInOutcome>> Run(string email = "customer@example.com", string password = Password) =>
        new PortalSignInCommandHandler(_accounts, _sessions, _hasher, _generator, _issuer, _customers, Settings.Default, Clock.Fixed(), _unitOfWork, _logger)
            .Handle(new PortalSignInCommand(email, password), Stubs.Ct);

    [Fact]
    public async Task SuccessReturnsThePortalTokensAndTheRefreshExpiry()
    {
        _accounts.Finds(Accounts.New());

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        SessionTokens tokens = result.Value.Tokens ?? throw new InvalidOperationException();
        Assert.Equal("portal-jwt", tokens.AccessToken);
        Assert.Equal("refresh-token", tokens.RefreshToken);
        Assert.Equal(Clock.Now + TimeSpan.FromDays(14), tokens.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task SuccessIssuesAPortalTokenNotAStaffTokenWithTheGrantFromTheContact()
    {
        _accounts.Finds(Accounts.New());
        ContactIs(CustomerFixtures.Match(isPrimary: true));

        await Run();

        PortalAccessTokenSubject subject = _subject ?? throw new InvalidOperationException();
        PortalSession session = Assert.Single(_added);
        Assert.Equal(Ids.Account, subject.AccountId);
        Assert.Equal("Layla Saleh", subject.DisplayName);
        Assert.Equal(Ids.TenantA.Value, subject.TenantId);
        Assert.Equal(session.Id.Value, subject.SessionId);
        Assert.Equal(Ids.Customer, subject.CustomerId);
        Assert.Equal(Ids.Contact, subject.ContactId);
        Assert.True(subject.IsAccountAdmin);
        Assert.Equal(Ids.BranchA1.Value, subject.BranchId);
        Assert.Equal(Ids.DeptA1.Value, subject.DepartmentId);
        _issuer.DidNotReceive().Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>());
        await _customers.Received(1).GetPortalContactAsync(Ids.Contact, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANonPrimaryContactIsNotAnAccountAdmin()
    {
        _accounts.Finds(Accounts.New());
        ContactIs(CustomerFixtures.Match(isPrimary: false));

        await Run();

        Assert.False((_subject ?? throw new InvalidOperationException()).IsAccountAdmin);
    }

    [Fact]
    public async Task SuccessAddsOneSessionWithTheGeneratedHashRaisesTheEventAndSavesOnce()
    {
        _accounts.Finds(Accounts.New());

        await Run();

        PortalSession session = Assert.Single(_added);
        Assert.Equal(new PortalAccountId(Ids.Account), session.PortalAccountId);
        Assert.Equal("refresh-hash", Assert.Single(session.Tokens).TokenHash);
        PortalSessionStarted started = Assert.IsType<PortalSessionStarted>(Assert.Single(session.DomainEvents));
        Assert.Equal(session.Id, started.SessionId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task AMalformedEmailIsTheGenericFailureWithADummyVerifyAndNoLookup(string email)
    {
        Result<SignInOutcome> result = await Run(email);

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        await _accounts.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Specification<PortalAccount>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownAccountIsTheGenericFailureWithADummyVerify()
    {
        _accounts.Finds(null);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "unknown"));
    }

    [Fact]
    public async Task ALockedAccountIsTheGenericFailureWithADummyVerifyAndNoSave()
    {
        PortalAccount account = Accounts.Failed(5);
        _accounts.Finds(account);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "locked"));
    }

    [Fact]
    public async Task AWrongPasswordSavesTheFailedAttemptOnce()
    {
        PortalAccount account = Accounts.New();
        _accounts.Finds(account);

        Result<SignInOutcome> result = await Run(password: "wrong");

        Outcome.Failed(result, "Identity.InvalidCredentials");
        Assert.Equal(1, account.FailedSignInCount);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "wrong-password"));
        Assert.Empty(_added);
    }

    [Fact]
    public async Task TheFifthWrongPasswordLocksTheAccountAndLogsAWarning()
    {
        PortalAccount account = Accounts.Failed(4);
        _accounts.Finds(account);

        await Run(password: "wrong");

        Assert.True(account.IsLockedOut(Clock.Now));
        Assert.True(_logger.Contains(LogLevel.Warning, "locked until"));
    }

    [Fact]
    public async Task ALostPortalAccessIsRefusedWithNoSessionAndNoSave()
    {
        _accounts.Finds(Accounts.New());
        _customers.GetPortalContactAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<PortalContactMatch>(Error.NotFound("Customers.PortalContactNotFound", "gone"))));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        Assert.Empty(_added);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "portal-access-revoked"));
    }

    [Fact]
    public async Task ADifferentContactTenantIsABindingMismatch()
    {
        _accounts.Finds(Accounts.New());
        ContactIs(CustomerFixtures.Match(tenant: Ids.TenantB));

        await AssertBindingMismatch();
    }

    [Fact]
    public async Task ADifferentContactCustomerIsABindingMismatch()
    {
        _accounts.Finds(Accounts.New());
        ContactIs(CustomerFixtures.Match(customerId: Ids.OtherCustomer));

        await AssertBindingMismatch();
    }

    [Fact]
    public async Task ADifferentContactIdIsABindingMismatch()
    {
        _accounts.Finds(Accounts.New());
        ContactIs(CustomerFixtures.Match(contactId: Ids.OtherContact));

        await AssertBindingMismatch();
    }

    private async Task AssertBindingMismatch()
    {
        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        Assert.Empty(_added);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _issuer.DidNotReceive().IssuePortal(Arg.Any<PortalAccessTokenSubject>(), Arg.Any<DateTimeOffset>());
        Assert.True(_logger.Contains(LogLevel.Information, "binding-mismatch"));
    }

    [Fact]
    public async Task ThereIsNoActiveCheckOnAPortalAccount()
    {
        Assert.Null(typeof(PortalAccount).GetProperty("IsActive"));
        _accounts.Finds(Accounts.New());

        Outcome.Succeeded(await Run());
    }

    [Fact]
    public async Task TheLookupUsesThePortalEmailSpecification()
    {
        _accounts.Finds(null);

        await Run();

        await _accounts.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<PortalAccount>>(s => s is PortalAccountByEmailSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoLogLineContainsTheEmailOrThePassword()
    {
        _accounts.Finds(Accounts.Failed(4));
        await Run(password: "Secret-pass-123");
        _accounts.Finds(null);
        await Run(password: "Secret-pass-123");
        await Run("not-an-email-address", "Secret-pass-123");
        _accounts.Finds(Accounts.New());
        await Run();

        Assert.NotEmpty(_logger.Entries);
        Assert.All(_logger.Entries, e =>
        {
            Assert.DoesNotContain("customer@example.com", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not-an-email", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret-pass-123", e.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Password, e.Message, StringComparison.Ordinal);
        });
    }
}
