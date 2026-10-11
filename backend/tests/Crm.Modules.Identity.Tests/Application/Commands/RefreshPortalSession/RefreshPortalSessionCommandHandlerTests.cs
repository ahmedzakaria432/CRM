using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.RefreshPortalSession;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Application.Commands.RefreshPortalSession;

public sealed class RefreshPortalSessionCommandHandlerTests
{
    private const string Presented = "presented-token";
    private const string PresentedHash = "presented-hash";

    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly IRepository<PortalSession, PortalSessionId> _sessions = Substitute.For<IRepository<PortalSession, PortalSessionId>>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly IAccessTokenIssuer _issuer = Substitute.For<IAccessTokenIssuer>();
    private readonly ICustomersModuleApi _customers = Substitute.For<ICustomersModuleApi>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly RecordingLogger<RefreshPortalSessionCommandHandler> _logger = new();
    private PortalAccessTokenSubject? _subject;

    public RefreshPortalSessionCommandHandlerTests()
    {
        _generator.Hash(Presented).Returns(PresentedHash);
        _generator.Generates("next-token", "next-hash");
        _accounts.Holds(Accounts.New());
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

    private Task<Result<SignInOutcome>> Run() =>
        new RefreshPortalSessionCommandHandler(_accounts, _sessions, _generator, _issuer, _customers, Settings.Default, Clock.Fixed(), _unitOfWork, _logger)
            .Handle(new RefreshPortalSessionCommand(Presented), Stubs.Ct);

    private PortalSession SessionHolding(DateTimeOffset? expires = null)
    {
        PortalSession session = Sessions.Start(hash: PresentedHash, now: Clock.Now.AddDays(-2), expires: expires);
        _sessions.Finds(session);
        return session;
    }

    [Fact]
    public async Task SuccessRotatesTheSessionAndReturnsTheNewRefreshToken()
    {
        PortalSession session = SessionHolding();

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        SessionTokens tokens = result.Value.Tokens ?? throw new InvalidOperationException();
        Assert.Equal("next-token", tokens.RefreshToken);
        Assert.Equal("portal-jwt", tokens.AccessToken);
        Assert.Equal(Clock.Now + TimeSpan.FromDays(14), tokens.RefreshTokenExpiresAtUtc);
        Assert.Equal(2, session.Tokens.Count);
        Assert.Equal(Clock.Now, session.Tokens.Single(t => t.TokenHash == PresentedHash).RotatedAtUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessIssuesAPortalTokenWithTheFreshGrant()
    {
        PortalSession session = SessionHolding();
        ContactIs(CustomerFixtures.Match(isPrimary: false));

        await Run();

        PortalAccessTokenSubject subject = _subject ?? throw new InvalidOperationException();
        Assert.Equal(session.Id.Value, subject.SessionId);
        Assert.False(subject.IsAccountAdmin);
        _issuer.DidNotReceive().Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ThePrimaryFlagIsReReadOnEveryRefresh()
    {
        SessionHolding();
        ContactIs(CustomerFixtures.Match(isPrimary: true));

        await Run();

        Assert.True((_subject ?? throw new InvalidOperationException()).IsAccountAdmin);
    }

    [Fact]
    public async Task AnUnknownTokenFailsWithoutSaving()
    {
        _sessions.Finds(null);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASpecificationCarryingTheHashFindsTheSession()
    {
        SessionHolding();

        await Run();

        await _sessions.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<PortalSession>>(s => s is PortalSessionByTokenHashSpecification), Arg.Any<CancellationToken>());
        _generator.Received(1).Hash(Presented);
    }

    [Fact]
    public async Task AMissingAccountRevokesTheSessionAsPortalAccessRevokedAndSaves()
    {
        PortalSession session = SessionHolding();
        _accounts.GetByIdAsync(Arg.Any<PortalAccountId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<PortalAccount?>(null));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.PortalAccessRevoked, session.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReuseRevokesTheSessionSavesAndLogsAWarning()
    {
        PortalSession session = SessionHolding();
        session.Rotate(PresentedHash, "earlier-next", Clock.Now.AddMinutes(-5), Clock.Now.AddDays(14));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.ReuseDetected, session.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Warning, "reuse detected"));
    }

    [Fact]
    public async Task AnExpiredTokenFailsWithoutSavingOrRevoking()
    {
        PortalSession session = SessionHolding(expires: Clock.Now);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.False(session.IsRevoked);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARevokedSessionFailsWithoutSaving()
    {
        PortalSession session = SessionHolding();
        session.Revoke(SessionRevocationReason.SignedOut, Clock.Now);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AContactLookupFailureRevokesTheSessionAndSaves()
    {
        PortalSession session = SessionHolding();
        _customers.GetPortalContactAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<PortalContactMatch>(Error.NotFound("Customers.PortalContactNotFound", "gone"))));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.PortalAccessRevoked, session.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _issuer.DidNotReceive().IssuePortal(Arg.Any<PortalAccessTokenSubject>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ABindingMismatchRevokesTheSessionAndSaves()
    {
        PortalSession session = SessionHolding();
        ContactIs(CustomerFixtures.Match(customerId: Ids.OtherCustomer));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.PortalAccessRevoked, session.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AStaffRefreshTokenIsUnknownToThePortalTable()
    {
        _sessions.Finds(null);
        _generator.Hash(Presented).Returns("staff-hash");

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<PortalAccountId>(), Arg.Any<CancellationToken>());
    }
}

public sealed class PortalSignOutCommandHandlerTests
{
    private readonly IRepository<PortalSession, PortalSessionId> _sessions = Substitute.For<IRepository<PortalSession, PortalSessionId>>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task<Result> Run(string? token) =>
        new Crm.Modules.Identity.Application.Commands.PortalSignOut.PortalSignOutCommandHandler(_sessions, _generator, Clock.Fixed(), _unitOfWork)
            .Handle(new Crm.Modules.Identity.Application.Commands.PortalSignOut.PortalSignOutCommand(token), Stubs.Ct);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ABlankTokenIsASuccessWithNoRepositoryCall(string? token)
    {
        Result result = await Run(token);

        Assert.True(result.IsSuccess);
        await _sessions.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Specification<PortalSession>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownTokenIsASuccessWithoutASave()
    {
        _generator.Hash("t").Returns("h");
        _sessions.Finds(null);

        Result result = await Run("t");

        Assert.True(result.IsSuccess);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AKnownTokenRevokesTheSessionAsSignedOutAndSavesOnce()
    {
        _generator.Hash("t").Returns("h");
        PortalSession session = Sessions.Start(hash: "h");
        _sessions.Finds(session);

        Result result = await Run("t");

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.SignedOut, session.RevocationReason);
        Assert.Equal(Clock.Now, session.RevokedAtUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAlreadyRevokedSessionKeepsItsFirstReason()
    {
        _generator.Hash("t").Returns("h");
        PortalSession session = Sessions.Revoked(SessionRevocationReason.PasswordReset);
        _sessions.Finds(session);

        Result result = await Run("t");

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.PasswordReset, session.RevocationReason);
    }
}
