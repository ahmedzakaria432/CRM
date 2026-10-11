using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.DomainEventHandlers;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Identity.Tests.Application.DomainEventHandlers;

public sealed class RecordSignInWhenSessionStartedTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task Run(DateTimeOffset at) =>
        new RecordSignInWhenSessionStarted(_users, _unitOfWork)
            .Handle(new DomainEventNotification<SessionStarted>(new SessionStarted(new UserId(Ids.User), new RefreshTokenFamilyId(Ids.Family), at)), Stubs.Ct);

    [Fact]
    public async Task AKnownUserHasTheSignInRecordedAtTheEventTimeAndIsSavedOnce()
    {
        User user = Users.Failed(3);
        _users.Holds(user);

        await Run(Clock.Now.AddHours(2));

        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedOutUntilUtc);
        Assert.Equal(Clock.Now.AddHours(2), user.LastSignInAtUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingUserReturnsWithoutSaving()
    {
        await Run(Clock.Now);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullNotificationIsAnArgumentError() =>
        await NullArguments.AssertThrowsArgumentNullAsync(new RecordSignInWhenSessionStarted(_users, _unitOfWork).Handle, null, Stubs.Ct);
}

public sealed class RecordPortalSignInWhenSessionStartedTests
{
    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task Run(DateTimeOffset at) =>
        new RecordPortalSignInWhenSessionStarted(_accounts, _unitOfWork)
            .Handle(new DomainEventNotification<PortalSessionStarted>(new PortalSessionStarted(new PortalAccountId(Ids.Account), new PortalSessionId(Ids.Session), at)), Stubs.Ct);

    [Fact]
    public async Task AKnownAccountHasTheSignInRecordedAtTheEventTimeAndIsSavedOnce()
    {
        PortalAccount account = Accounts.Failed(3);
        _accounts.Holds(account);

        await Run(Clock.Now.AddHours(2));

        Assert.Equal(0, account.FailedSignInCount);
        Assert.Equal(Clock.Now.AddHours(2), account.LastSignInAtUtc);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingAccountReturnsWithoutSaving()
    {
        await Run(Clock.Now);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullNotificationIsAnArgumentError() =>
        await NullArguments.AssertThrowsArgumentNullAsync(new RecordPortalSignInWhenSessionStarted(_accounts, _unitOfWork).Handle, null, Stubs.Ct);
}

public sealed class RevokeSessionsWhenUserDeactivatedTests
{
    private readonly IRepository<RefreshTokenFamily, RefreshTokenFamilyId> _families = Substitute.For<IRepository<RefreshTokenFamily, RefreshTokenFamilyId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task Run() =>
        new RevokeSessionsWhenUserDeactivated(_families, Clock.Fixed(), _unitOfWork)
            .Handle(new DomainEventNotification<UserDeactivated>(new UserDeactivated(new UserId(Ids.User), Clock.Now.AddDays(-1))), Stubs.Ct);

    [Fact]
    public async Task EveryActiveFamilyIsRevokedAsUserDeactivatedAtTheClockTime()
    {
        RefreshTokenFamily one = Families.Start(id: RefreshTokenFamilyId.New());
        RefreshTokenFamily two = Families.Start(id: RefreshTokenFamilyId.New());
        _families.Lists(one, two);

        await Run();

        Assert.All([one, two], f =>
        {
            Assert.Equal(SessionRevocationReason.UserDeactivated, f.RevocationReason);
            Assert.Equal(Clock.Now, f.RevokedAtUtc);
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAlreadyRevokedFamilyIsNotInTheSpecificationResultAndStaysUntouched()
    {
        RefreshTokenFamily live = Families.Start(id: RefreshTokenFamilyId.New());
        RefreshTokenFamily revoked = Families.Start(id: RefreshTokenFamilyId.New());
        revoked.Revoke(SessionRevocationReason.SignedOut, Clock.Now.AddDays(-3));
        _families.Lists(live, revoked);

        await Run();

        Assert.Equal(SessionRevocationReason.UserDeactivated, live.RevocationReason);
        Assert.Equal(SessionRevocationReason.SignedOut, revoked.RevocationReason);
        Assert.Equal(Clock.Now.AddDays(-3), revoked.RevokedAtUtc);
    }

    [Fact]
    public async Task NoFamiliesStillSaves()
    {
        _families.Lists();

        await Run();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheSpecificationIsTheActiveFamiliesOfTheEventUser()
    {
        _families.Lists();

        await Run();

        await _families.Received(1).ListAsync(Arg.Is<Specification<RefreshTokenFamily>>(s => s is ActiveRefreshTokenFamiliesOfUserSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AFamilyOfAnotherUserIsNotMatchedBySpecification()
    {
        RefreshTokenFamily foreign = Families.Start(userId: new UserId(Ids.OtherUser), id: RefreshTokenFamilyId.New());
        _families.Lists(foreign);

        await Run();

        Assert.False(foreign.IsRevoked);
    }

    [Fact]
    public async Task ANullNotificationIsAnArgumentError() =>
        await NullArguments.AssertThrowsArgumentNullAsync(new RevokeSessionsWhenUserDeactivated(_families, Clock.Fixed(), _unitOfWork).Handle, null, Stubs.Ct);
}

public sealed class RevokeSessionsWhenPasswordChangedTests
{
    private readonly IRepository<RefreshTokenFamily, RefreshTokenFamilyId> _families = Substitute.For<IRepository<RefreshTokenFamily, RefreshTokenFamilyId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task Run(Guid? keep) =>
        new RevokeSessionsWhenPasswordChanged(_families, Clock.Fixed(), _unitOfWork)
            .Handle(new DomainEventNotification<UserPasswordChanged>(new UserPasswordChanged(new UserId(Ids.User), keep, Clock.Now.AddDays(-1))), Stubs.Ct);

    [Fact]
    public async Task ANullKeepSessionRevokesEveryActiveFamilyAsPasswordReset()
    {
        RefreshTokenFamily one = Families.Start(id: RefreshTokenFamilyId.New());
        RefreshTokenFamily two = Families.Start(id: RefreshTokenFamilyId.New());
        _families.Lists(one, two);

        await Run(null);

        Assert.All([one, two], f =>
        {
            Assert.Equal(SessionRevocationReason.PasswordReset, f.RevocationReason);
            Assert.Equal(Clock.Now, f.RevokedAtUtc);
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AKeptSessionIsUntouchedAndTheOthersAreRevokedAsPasswordChanged()
    {
        RefreshTokenFamily kept = Families.Start(id: new RefreshTokenFamilyId(Ids.Family));
        RefreshTokenFamily other = Families.Start(id: RefreshTokenFamilyId.New());
        _families.Lists(kept, other);

        await Run(Ids.Family);

        Assert.False(kept.IsRevoked);
        Assert.Equal(SessionRevocationReason.PasswordChanged, other.RevocationReason);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AKeptIdMatchingNoFamilyRevokesAll()
    {
        RefreshTokenFamily one = Families.Start(id: RefreshTokenFamilyId.New());
        _families.Lists(one);

        await Run(Guid.NewGuid());

        Assert.Equal(SessionRevocationReason.PasswordChanged, one.RevocationReason);
    }

    [Fact]
    public async Task NoFamiliesStillSaves()
    {
        _families.Lists();

        await Run(null);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullNotificationIsAnArgumentError() =>
        await NullArguments.AssertThrowsArgumentNullAsync(new RevokeSessionsWhenPasswordChanged(_families, Clock.Fixed(), _unitOfWork).Handle, null, Stubs.Ct);
}

public sealed class RevokePortalSessionsWhenPasswordChangedTests
{
    private readonly IRepository<PortalSession, PortalSessionId> _sessions = Substitute.For<IRepository<PortalSession, PortalSessionId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task Run(Guid? keep) =>
        new RevokePortalSessionsWhenPasswordChanged(_sessions, Clock.Fixed(), _unitOfWork)
            .Handle(new DomainEventNotification<PortalPasswordChanged>(new PortalPasswordChanged(new PortalAccountId(Ids.Account), keep, Clock.Now.AddDays(-1))), Stubs.Ct);

    [Fact]
    public async Task ANullKeepSessionRevokesEverySessionAsPasswordReset()
    {
        PortalSession one = Sessions.Start(id: PortalSessionId.New());
        PortalSession two = Sessions.Start(id: PortalSessionId.New());
        _sessions.Lists(one, two);

        await Run(null);

        Assert.All([one, two], s => Assert.Equal(SessionRevocationReason.PasswordReset, s.RevocationReason));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AKeptSessionIsUntouchedAndTheOthersAreRevokedAsPasswordChanged()
    {
        PortalSession kept = Sessions.Start(id: new PortalSessionId(Ids.Session));
        PortalSession other = Sessions.Start(id: PortalSessionId.New());
        _sessions.Lists(kept, other);

        await Run(Ids.Session);

        Assert.False(kept.IsRevoked);
        Assert.Equal(SessionRevocationReason.PasswordChanged, other.RevocationReason);
        Assert.Equal(Clock.Now, other.RevokedAtUtc);
    }

    [Fact]
    public async Task AKeptIdMatchingNoSessionRevokesAll()
    {
        PortalSession one = Sessions.Start(id: PortalSessionId.New());
        _sessions.Lists(one);

        await Run(Guid.NewGuid());

        Assert.True(one.IsRevoked);
    }

    [Fact]
    public async Task NoSessionsStillSaves()
    {
        _sessions.Lists();

        await Run(null);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullNotificationIsAnArgumentError() =>
        await NullArguments.AssertThrowsArgumentNullAsync(new RevokePortalSessionsWhenPasswordChanged(_sessions, Clock.Fixed(), _unitOfWork).Handle, null, Stubs.Ct);
}

public sealed class DomainEventFlowTests
{
    [Fact]
    public async Task APasswordChangeRevokesTheSiblingSessionWhileKeepingTheCurrentOne()
    {
        string database = IdentityTestContext.NewDatabaseName();
        var userId = new UserId(Ids.User);
        var currentFamily = new RefreshTokenFamilyId(Ids.Family);
        var siblingFamily = RefreshTokenFamilyId.New();

        await using (IdentityDbContext store = IdentityTestContext.Store(database))
        {
            store.Users.Add(Users.New());
            store.RefreshTokenFamilies.AddRange(
                Families.Start(userId, "h-current", id: currentFamily),
                Families.Start(userId, "h-sibling", id: siblingFamily));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        UserPasswordChanged raised;
        await using (IdentityDbContext context = IdentityTestContext.Create(database, CurrentUsers.Admin()))
        {
            User user = await context.Users.SingleAsync(Stubs.Ct);
            user.ChangePassword("hash-2", currentFamily.Value, Clock.Now);
            raised = Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents));
            await context.SaveChangesAsync(Stubs.Ct);

            var handler = new RevokeSessionsWhenPasswordChanged(
                IdentityTestContext.Repo<RefreshTokenFamily, RefreshTokenFamilyId>(context), Clock.Fixed(), context);
            await handler.Handle(new DomainEventNotification<UserPasswordChanged>(raised), Stubs.Ct);
        }

        await using IdentityDbContext read = IdentityTestContext.Store(database);
        RefreshTokenFamily current = await read.RefreshTokenFamilies.SingleAsync(f => f.Id == currentFamily, Stubs.Ct);
        RefreshTokenFamily sibling = await read.RefreshTokenFamilies.SingleAsync(f => f.Id == siblingFamily, Stubs.Ct);
        Assert.False(current.IsRevoked);
        Assert.True(sibling.IsRevoked);
        Assert.Equal(SessionRevocationReason.PasswordChanged, sibling.RevocationReason);
    }
}
