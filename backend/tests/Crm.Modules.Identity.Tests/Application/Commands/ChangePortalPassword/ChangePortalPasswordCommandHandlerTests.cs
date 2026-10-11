using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.ChangePortalPassword;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Tests.Application.Commands.ChangePortalPassword;

public sealed class ChangePortalPasswordCommandHandlerTests
{
    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    public ChangePortalPasswordCommandHandlerTests()
    {
        _hasher.Verify("hash-1", "current-pass-1").Returns(true);
        _hasher.Hash("new-pass-12345").Returns("hash-new");
    }

    private Task<Result> Run(ICurrentUserService currentUser) =>
        new ChangePortalPasswordCommandHandler(_accounts, _hasher, currentUser, Clock.Fixed(), _unitOfWork)
            .Handle(new ChangePortalPasswordCommand("current-pass-1", "new-pass-12345"), Stubs.Ct);

    private static ICurrentUserService Portal(string? userId = null, string? sessionId = null, Guid? contact = null) =>
        CurrentUsers.WithIds(
            Scopes.Portal(Ids.TenantA, Ids.Customer, contact ?? Ids.Contact, true),
            userId ?? Ids.Account.ToString(),
            sessionId ?? CurrentUsers.DefaultSessionId);

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task ANonGuidUserIsPortalOnlyWithNoRepositoryCall(string? userId)
    {
        Result result = await Run(CurrentUsers.WithIds(Scopes.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, true), userId, null));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<PortalAccountId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AStaffPrincipalWithNoPortalGrantIsPortalOnly()
    {
        Result result = await Run(CurrentUsers.Agent(Scope.OfTenantA()));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<PortalAccountId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingAccountIsPortalOnly()
    {
        Result result = await Run(Portal());

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task AGrantContactThatDiffersFromTheAccountsContactIsPortalOnlyWithNoVerify()
    {
        _accounts.Holds(Accounts.New());

        Result result = await Run(Portal(contact: Ids.OtherContact));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AWrongCurrentPasswordIsAValidationFailureOnTheField()
    {
        _accounts.Holds(Accounts.New(hash: "hash-other"));

        Result result = await Run(Portal());

        Expect.Failure(result, "Identity.CurrentPasswordInvalid", ErrorType.Validation);
        Assert.StartsWith("CurrentPassword:", result.Error.Description, StringComparison.Ordinal);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessRaisesTheEventKeepingTheCallersSessionAndSavesOnce()
    {
        PortalAccount account = Accounts.New();
        _accounts.Holds(account);

        Result result = await Run(Portal());

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-new", account.PasswordHash);
        PortalPasswordChanged raised = Assert.IsType<PortalPasswordChanged>(Assert.Single(account.DomainEvents));
        Assert.Equal(Ids.Session, raised.KeepSessionId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInvalidSessionIdKeepsNoSession()
    {
        PortalAccount account = Accounts.New();
        _accounts.Holds(account);

        await Run(Portal(sessionId: "not-a-guid"));

        Assert.Null(Assert.IsType<PortalPasswordChanged>(Assert.Single(account.DomainEvents)).KeepSessionId);
    }
}
