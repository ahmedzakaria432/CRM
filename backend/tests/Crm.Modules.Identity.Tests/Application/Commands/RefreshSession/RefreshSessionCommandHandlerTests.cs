using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.RefreshSession;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Application.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandlerTests
{
    private const string Presented = "presented-token";
    private const string PresentedHash = "presented-hash";

    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IRepository<RefreshTokenFamily, RefreshTokenFamilyId> _families = Substitute.For<IRepository<RefreshTokenFamily, RefreshTokenFamilyId>>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly IAccessTokenIssuer _issuer = Substitute.For<IAccessTokenIssuer>();
    private readonly IOrganisationModuleApi _organisation = Substitute.For<IOrganisationModuleApi>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly RecordingLogger<RefreshSessionCommandHandler> _logger = new();
    private AccessTokenSubject? _subject;

    public RefreshSessionCommandHandlerTests()
    {
        _generator.Hash(Presented).Returns(PresentedHash);
        _generator.Generates("next-token", "next-hash");
        _users.Holds(Users.New());
        _issuer.Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>())
            .Returns(call =>
            {
                _subject = call.Arg<AccessTokenSubject>();
                return new IssuedAccessToken("jwt", Clock.Now.AddMinutes(15));
            });
        _organisation.GetMembershipsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(Memberships.Of(Ids.User, Ids.TenantA, (Ids.DeptA1, Ids.BranchA1, "Agent")))));
    }

    private Task<Result<SignInOutcome>> Run() =>
        new RefreshSessionCommandHandler(_users, _families, _generator, _issuer, _organisation, Settings.Default, Clock.Fixed(), _unitOfWork, _logger)
            .Handle(new RefreshSessionCommand(Presented), Stubs.Ct);

    private RefreshTokenFamily FamilyHolding(string hash = PresentedHash, DateTimeOffset? expires = null)
    {
        RefreshTokenFamily family = Families.Start(hash: hash, now: Clock.Now.AddDays(-2), expires: expires);
        _families.Finds(family);
        return family;
    }

    [Fact]
    public async Task SuccessRotatesTheFamilyAndReturnsTheNewRefreshToken()
    {
        RefreshTokenFamily family = FamilyHolding();

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        SessionTokens tokens = result.Value.Tokens ?? throw new InvalidOperationException();
        Assert.Equal("next-token", tokens.RefreshToken);
        Assert.Equal("jwt", tokens.AccessToken);
        Assert.Equal(Clock.Now + TimeSpan.FromDays(14), tokens.RefreshTokenExpiresAtUtc);
        Assert.Equal(2, family.Tokens.Count);
        Assert.Equal(Clock.Now, family.Tokens.Single(t => t.TokenHash == PresentedHash).RotatedAtUtc);
        Assert.Contains(family.Tokens, t => t.TokenHash == "next-hash" && t.RotatedAtUtc is null);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessIssuesForTheFamilyIdAndRereadsTheMemberships()
    {
        RefreshTokenFamily family = FamilyHolding();

        await Run();

        AccessTokenSubject subject = _subject ?? throw new InvalidOperationException();
        Assert.Equal(family.Id.Value, subject.SessionId);
        Assert.Equal(Ids.User, subject.UserId);
        Assert.Single(subject.Memberships);
        await _organisation.Received(1).GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheSpecificationCarriesTheHashOfThePresentedToken()
    {
        FamilyHolding();

        await Run();

        _generator.Received(1).Hash(Presented);
        await _families.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<RefreshTokenFamily>>(s => s is RefreshTokenFamilyByTokenHashSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownTokenFailsWithoutSaving()
    {
        _families.Finds(null);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingUserRevokesTheFamilyAndSaves()
    {
        RefreshTokenFamily family = FamilyHolding();
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<User?>(null));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.UserDeactivated, family.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInactiveUserRevokesTheFamilyAndSaves()
    {
        RefreshTokenFamily family = FamilyHolding();
        _users.Holds(Users.Inactive());

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.UserDeactivated, family.RevocationReason);
        Assert.Equal(Clock.Now, family.RevokedAtUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _issuer.DidNotReceive().Issue(Arg.Any<AccessTokenSubject>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ReuseRevokesTheFamilySavesAndLogsAWarning()
    {
        RefreshTokenFamily family = FamilyHolding();
        family.Rotate(PresentedHash, "earlier-next", Clock.Now.AddMinutes(-5), Clock.Now.AddDays(14));

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.Equal(SessionRevocationReason.ReuseDetected, family.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Warning, "reuse detected"));
    }

    [Fact]
    public async Task AnExpiredTokenFailsWithoutSavingAndWithoutRevoking()
    {
        RefreshTokenFamily family = FamilyHolding(expires: Clock.Now);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        Assert.False(family.IsRevoked);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARevokedFamilyFailsWithoutSaving()
    {
        RefreshTokenFamily family = FamilyHolding();
        family.Revoke(SessionRevocationReason.SignedOut, Clock.Now);

        Result<SignInOutcome> result = await Run();

        Outcome.Failed(result, "Identity.InvalidRefreshToken");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMembershipsFailureIsReturnedAndNothingIsSaved()
    {
        FamilyHolding();
        _organisation.GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<UserMemberships>(Error.Failure("Organisation.Boom", "boom"))));

        Result<SignInOutcome> result = await Run();

        Expect.Failure(result, "Organisation.Boom", ErrorType.Failure);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MembershipsOfAnotherTenantAreDropped()
    {
        FamilyHolding();
        _organisation.GetMembershipsAsync(Ids.User, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(Memberships.Of(Ids.User, Ids.TenantB, (Ids.DeptA1, Ids.BranchA1, "Administrator")))));

        Result<SignInOutcome> result = await Run();

        Outcome.Succeeded(result);
        Assert.Empty((_subject ?? throw new InvalidOperationException()).Memberships);
    }
}

public sealed class SignOutCommandHandlerTests
{
    private readonly IRepository<RefreshTokenFamily, RefreshTokenFamilyId> _families = Substitute.For<IRepository<RefreshTokenFamily, RefreshTokenFamilyId>>();
    private readonly IRefreshTokenGenerator _generator = Substitute.For<IRefreshTokenGenerator>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task<Result> Run(string? token) =>
        new Crm.Modules.Identity.Application.Commands.SignOut.SignOutCommandHandler(_families, _generator, Clock.Fixed(), _unitOfWork)
            .Handle(new Crm.Modules.Identity.Application.Commands.SignOut.SignOutCommand(token), Stubs.Ct);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankTokenIsASuccessWithNoRepositoryCallAndNoSave(string? token)
    {
        Result result = await Run(token);

        Assert.True(result.IsSuccess);
        await _families.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Specification<RefreshTokenFamily>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownTokenIsASuccessWithoutASave()
    {
        _generator.Hash("t").Returns("h");
        _families.Finds(null);

        Result result = await Run("t");

        Assert.True(result.IsSuccess);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AKnownTokenRevokesTheFamilyAsSignedOutAndSavesOnce()
    {
        _generator.Hash("t").Returns("h");
        RefreshTokenFamily family = Families.Start(hash: "h");
        _families.Finds(family);

        Result result = await Run("t");

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.SignedOut, family.RevocationReason);
        Assert.Equal(Clock.Now, family.RevokedAtUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _families.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<RefreshTokenFamily>>(s => s is RefreshTokenFamilyByTokenHashSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAlreadyRevokedFamilyKeepsItsFirstReasonAndStillSaves()
    {
        _generator.Hash("t").Returns("h");
        RefreshTokenFamily family = Families.Revoked(SessionRevocationReason.PasswordReset);
        _families.Finds(family);

        Result result = await Run("t");

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.PasswordReset, family.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
