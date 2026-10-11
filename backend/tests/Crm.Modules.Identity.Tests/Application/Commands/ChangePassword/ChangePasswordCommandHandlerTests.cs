using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.ChangePassword;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Tests.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    public ChangePasswordCommandHandlerTests()
    {
        _hasher.Verify("hash-1", "current-pass-1").Returns(true);
        _hasher.Hash("new-pass-12345").Returns("hash-new");
    }

    private Task<Result> Run(ICurrentUserService currentUser) =>
        new ChangePasswordCommandHandler(_users, _hasher, currentUser, Clock.Fixed(), _unitOfWork)
            .Handle(new ChangePasswordCommand("current-pass-1", "new-pass-12345"), Stubs.Ct);

    private static ICurrentUserService Caller(string? userId, string? sessionId) =>
        CurrentUsers.WithIds(Scope.OfTenantA(), userId, sessionId);

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task ANonGuidUserIsUnauthenticatedWithNoRepositoryCall(string? userId)
    {
        Result result = await Run(Caller(userId, null));

        Expect.Failure(result, "Authorization.Unauthenticated", ErrorType.Unauthorized);
        await _users.DidNotReceiveWithAnyArgs().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingUserIsUnauthenticated()
    {
        Result result = await Run(CurrentUsers.Agent(Scope.OfTenantA()));

        Expect.Failure(result, "Authorization.Unauthenticated", ErrorType.Unauthorized);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AWrongCurrentPasswordIsAValidationFailureOnTheField()
    {
        _users.Holds(Users.New(hash: "hash-other"));

        Result result = await Run(CurrentUsers.Agent(Scope.OfTenantA()));

        Expect.Failure(result, "Identity.CurrentPasswordInvalid", ErrorType.Validation);
        Assert.StartsWith("CurrentPassword:", result.Error.Description, StringComparison.Ordinal);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task SuccessHashesTheNewPasswordRaisesTheEventKeepingTheCallersSessionAndSavesOnce()
    {
        User user = Users.New();
        _users.Holds(user);

        Result result = await Run(CurrentUsers.Agent(Scope.OfTenantA()));

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-new", user.PasswordHash);
        UserPasswordChanged raised = Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents));
        Assert.Equal(Ids.Session, raised.KeepSessionId);
        Assert.Equal(Clock.Now, raised.OccurredOnUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AMissingOrInvalidSessionKeepsNoSession(string? sessionId)
    {
        User user = Users.New();
        _users.Holds(user);

        Result result = await Run(Caller(CurrentUsers.DefaultUserId, sessionId));

        Assert.True(result.IsSuccess);
        Assert.Null(Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents)).KeepSessionId);
    }

    [Fact]
    public async Task TheHandlerNeverComparesTheNewPasswordWithTheCurrentOne()
    {
        User user = Users.New();
        _users.Holds(user);
        _hasher.Hash("current-pass-1").Returns("hash-again");

        Result result = await new ChangePasswordCommandHandler(_users, _hasher, CurrentUsers.Agent(Scope.OfTenantA()), Clock.Fixed(), _unitOfWork)
            .Handle(new ChangePasswordCommand("current-pass-1", "current-pass-1"), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-again", user.PasswordHash);
    }
}
