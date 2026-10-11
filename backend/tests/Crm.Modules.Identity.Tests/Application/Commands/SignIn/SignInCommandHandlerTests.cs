using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.SignIn;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Application.Commands.SignIn;

public sealed class SignInCommandHandlerTests
{
    private const string Password = "Correct-horse-9";

    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IRepository<RefreshTokenFamily, RefreshTokenFamilyId> _families = Substitute.For<IRepository<RefreshTokenFamily, RefreshTokenFamilyId>>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly IAccessTokenIssuer _issuer = Substitute.For<IAccessTokenIssuer>();
    private readonly IOrganisationModuleApi _organisation = Substitute.For<IOrganisationModuleApi>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly RecordingLogger<SignInCommandHandler> _logger = new();
    private readonly List<RefreshTokenFamily> _addedFamilies = [];
    private AccessTokenSubject? _subject;

    public SignInCommandHandlerTests()
    {
        _hasher.Verify("hash-1", Password).Returns(true);
        _generator.Generates("refresh-token", "refresh-hash");
        _families.When(r => r.Add(Arg.Any<RefreshTokenFamily>())).Do(call => _addedFamilies.Add(call.Arg<RefreshTokenFamily>()));
        _issuer.Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>())
            .Returns(call =>
            {
                _subject = call.Arg<AccessTokenSubject>();
                return new IssuedAccessToken("jwt", Clock.Now.AddMinutes(15));
            });
        _organisation.GetMembershipsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(Memberships.Of(Ids.User, Ids.TenantA, (Ids.DeptA1, Ids.BranchA1, "Agent")))));
    }

    private SignInCommandHandler Handler() =>
        new(_users, _families, _hasher, _generator, _issuer, _organisation, Settings.Default, Clock.Fixed(), _unitOfWork, _logger);

    private Task<Result<SignInOutcome>> Run(string email = "agent@example.com", string password = Password) =>
        Handler().Handle(new SignInCommand(email, password), Stubs.Ct);

    [Fact]
    public async Task SuccessReturnsTheTokensWithTheRefreshExpiry()
    {
        User user = Users.New();
        _users.Finds(user);

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        SessionTokens tokens = result.Value.Tokens ?? throw new InvalidOperationException();
        Assert.Equal("jwt", tokens.AccessToken);
        Assert.Equal(Clock.Now.AddMinutes(15), tokens.AccessTokenExpiresAtUtc);
        Assert.Equal("refresh-token", tokens.RefreshToken);
        Assert.Equal(Clock.Now + TimeSpan.FromDays(14), tokens.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task SuccessAddsOneFamilyForTheUserWithTheGeneratedHashAndSavesOnce()
    {
        _users.Finds(Users.New());

        await Run();

        RefreshTokenFamily family = Assert.Single(_addedFamilies);
        Assert.Equal(new UserId(Ids.User), family.UserId);
        Assert.Equal("refresh-hash", Assert.Single(family.Tokens).TokenHash);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessVerifiesTheStoredHashAgainstThePassword()
    {
        _users.Finds(Users.New());

        await Run();

        _hasher.Received(1).Verify("hash-1", Password);
        _hasher.DidNotReceive().VerifyAgainstDummy(Arg.Any<string>());
    }

    [Fact]
    public async Task SuccessIssuesTheTokenForTheFamilyIdAsSessionIdWithTheMemberships()
    {
        _users.Finds(Users.New());

        await Run();

        AccessTokenSubject subject = _subject ?? throw new InvalidOperationException();
        RefreshTokenFamily family = Assert.Single(_addedFamilies);
        Assert.Equal(Ids.User, subject.UserId);
        Assert.Equal("Omar Haddad", subject.DisplayName);
        Assert.Equal(Ids.TenantA.Value, subject.TenantId);
        Assert.Equal(family.Id.Value, subject.SessionId);
        TokenMembership membership = Assert.Single(subject.Memberships);
        Assert.Equal(new TokenMembership(Ids.DeptA1.Value, Ids.BranchA1.Value, "Agent"), membership);
    }

    [Fact]
    public async Task SuccessIssuesAtTheClockTime()
    {
        _users.Finds(Users.New());

        await Run();

        _issuer.Received(1).Issue(Arg.Any<AccessTokenSubject>(), Clock.Now);
    }

    [Fact]
    public async Task AUserWithNoMembershipsSignsInWithAnEmptyList()
    {
        _users.Finds(Users.New());
        _organisation.GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new UserMemberships(Ids.User, Guid.Empty, []))));

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        Assert.Empty((_subject ?? throw new InvalidOperationException()).Memberships);
    }

    [Fact]
    public async Task MembershipsOfAnotherTenantAreDroppedButSignInSucceeds()
    {
        _users.Finds(Users.New());
        _organisation.GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(Memberships.Of(Ids.User, Ids.TenantB, (Ids.DeptA1, Ids.BranchA1, "Administrator")))));

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        Assert.Empty((_subject ?? throw new InvalidOperationException()).Memberships);
    }

    [Fact]
    public async Task MembershipsOfTheSameTenantAreKept()
    {
        _users.Finds(Users.New());
        _organisation.GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(Memberships.Of(
                Ids.User, Ids.TenantA, (Ids.DeptA1, Ids.BranchA1, "Agent"), (Ids.DeptA2, Ids.BranchA2, "Supervisor")))));

        await Run();

        Assert.Equal(2, (_subject ?? throw new InvalidOperationException()).Memberships.Count);
    }

    [Fact]
    public async Task AMembershipsFailureIsReturnedAsIsWithNoFamilyAndNoSave()
    {
        _users.Finds(Users.New());
        var error = Error.Forbidden("Organisation.OutOfScope", "nope");
        _organisation.GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Failure<UserMemberships>(error)));

        Result<SignInOutcome> result = await Run();

        Expect.Failure(result, "Organisation.OutOfScope", ErrorType.Forbidden);
        Assert.Empty(_addedFamilies);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _issuer.DidNotReceive().Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("")]
    public async Task AMalformedEmailIsTheGenericFailureWithADummyVerifyAndNoLookup(string email)
    {
        Result<SignInOutcome> result = await Run(email);

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        await _users.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Specification<User>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "malformed"));
    }

    [Fact]
    public async Task AnUnknownEmailIsTheGenericFailureWithADummyVerifyAndNoSave()
    {
        _users.Finds(null);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "unknown"));
    }

    [Fact]
    public async Task AnInactiveUserIsTheGenericFailureWithADummyVerifyAndNoVerify()
    {
        _users.Finds(Users.Inactive());

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "inactive"));
    }

    [Fact]
    public async Task ALockedUserIsTheGenericFailureWithADummyVerifyAndNoSave()
    {
        _users.Finds(Users.Locked(Clock.Now.AddMinutes(5)));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidCredentials");
        _hasher.Received(1).VerifyAgainstDummy(Password);
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "locked"));
    }

    [Fact]
    public async Task ALockThatExpiredExactlyNowAllowsSignIn()
    {
        _users.Finds(Users.Locked(Clock.Now));

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
    }

    [Fact]
    public async Task AWrongPasswordIsTheGenericFailureAndTheFailedAttemptIsSavedOnce()
    {
        User user = Users.New();
        _users.Finds(user);

        Result<SignInOutcome> result = await Run(password: "wrong");

        Outcome.Failed(result, "Identity.InvalidCredentials");
        Assert.Equal(1, user.FailedSignInCount);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "wrong-password"));
        _hasher.DidNotReceive().VerifyAgainstDummy(Arg.Any<string>());
        Assert.Empty(_addedFamilies);
    }

    [Fact]
    public async Task TheAttemptThatReachesTheLimitLocksTheUserAndLogsAWarning()
    {
        User user = Users.Failed(4);
        _users.Finds(user);

        Result<SignInOutcome> result = await Run(password: "wrong");

        Outcome.Failed(result, "Identity.InvalidCredentials");
        Assert.True(user.IsLockedOut(Clock.Now));
        Assert.True(_logger.Contains(LogLevel.Warning, "locked until"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefusalPathsNeverReachTheOrganisationOrTheIssuer()
    {
        _users.Finds(null);
        await Run();
        _users.Finds(Users.Inactive());
        await Run();
        _users.Finds(Users.Locked(Clock.Now.AddMinutes(5)));
        await Run();
        _users.Finds(Users.New());
        await Run(password: "wrong");

        await _organisation.DidNotReceive().GetMembershipsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _issuer.DidNotReceive().Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task EveryRefusalYieldsTheIdenticalOutcome()
    {
        var outcomes = new List<SignInOutcome>();

        outcomes.Add((await Run("bad")).Value);
        _users.Finds(null);
        outcomes.Add((await Run()).Value);
        _users.Finds(Users.Inactive());
        outcomes.Add((await Run()).Value);
        _users.Finds(Users.Locked(Clock.Now.AddMinutes(5)));
        outcomes.Add((await Run()).Value);
        _users.Finds(Users.New());
        outcomes.Add((await Run(password: "wrong")).Value);

        Assert.All(outcomes, o => Assert.Equal(SignInOutcome.Failed("Identity.InvalidCredentials"), o));
    }

    [Fact]
    public async Task TheLookupUsesTheEmailSpecification()
    {
        _users.Finds(null);

        await Run(" AGENT@Example.com ");

        await _users.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<User>>(s => s is UserByEmailSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoLogLineContainsTheEmailOrThePassword()
    {
        User user = Users.Failed(4);
        _users.Finds(user);
        await Run(password: "Secret-pass-123");
        _users.Finds(null);
        await Run(password: "Secret-pass-123");
        await Run("not-an-email-address", "Secret-pass-123");
        _users.Finds(Users.New());
        await Run();

        Assert.NotEmpty(_logger.Entries);
        Assert.All(_logger.Entries, e =>
        {
            Assert.DoesNotContain("agent@example.com", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not-an-email", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret-pass-123", e.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Password, e.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ASuccessfulSignInLogsTheUserAndSessionIds()
    {
        _users.Finds(Users.New());

        await Run();

        Assert.True(_logger.Contains(LogLevel.Information, Ids.User.ToString()));
    }

    [Fact]
    public void TheHandlerInjectsNoEventBus()
    {
        Assert.DoesNotContain(typeof(SignInCommandHandler).GetConstructors().Single().GetParameters(), p => p.ParameterType.Name.Contains("EventBus", StringComparison.Ordinal));
    }
}
