using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Domain;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Error.Code;

    [Fact]
    public void CreateStartsActiveWithZeroCountersAndNoEvent()
    {
        User user = Users.New(Ids.TenantB, "x@y.com", "Name", "ar", "hash-x");

        Assert.Equal(Ids.TenantB, user.TenantId);
        Assert.Equal("x@y.com", user.Email.Value);
        Assert.Equal("Name", user.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, user.PreferredLocale);
        Assert.Equal("hash-x", user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedOutUntilUtc);
        Assert.Null(user.LastSignInAtUtc);
        Assert.Empty(user.RowVersion);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void CreateUsesTheSuppliedIdOrGeneratesOne()
    {
        var id = UserId.New();

        Assert.Equal(id, Users.New(id: id).Id);
        Assert.NotEqual(Guid.Empty, User.Create(Ids.TenantA, Emails.Of(), Names.Of(), PreferredLocale.English, "h").Id.Value);
    }

    [Fact]
    public void CreateRefusesTheNoneTenant() =>
        Assert.Equal("Identity.TenantRequired", CodeOf(() => Users.New(TenantId.None)));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void CreateRefusesABlankHash(string hash) =>
        Assert.Equal("Identity.PasswordHashRequired", CodeOf(() => Users.New(hash: hash)));

    [Fact]
    public void CreateGuardsNullArguments()
    {
        NullArguments.AssertThrowsArgumentNull(User.Create, null, Emails.Of(), Names.Of(), PreferredLocale.English, "h", null);
        NullArguments.AssertThrowsArgumentNull(User.Create, Ids.TenantA, null, Names.Of(), PreferredLocale.English, "h", null);
        NullArguments.AssertThrowsArgumentNull(User.Create, Ids.TenantA, Emails.Of(), null, PreferredLocale.English, "h", null);
        NullArguments.AssertThrowsArgumentNull(User.Create, Ids.TenantA, Emails.Of(), Names.Of(), null, "h", null);
    }

    [Fact]
    public void IsLockedOutFollowsTheLockInstant()
    {
        Assert.False(Users.New().IsLockedOut(Now));
        Assert.True(Users.Locked(Now.AddTicks(1)).IsLockedOut(Now));
        Assert.False(Users.Locked(Now).IsLockedOut(Now));
        Assert.False(Users.Locked(Now.AddTicks(-1)).IsLockedOut(Now));
    }

    [Fact]
    public void FailedSignInIncrementsBelowTheLimit()
    {
        User user = Users.Failed(4);

        Assert.Equal(4, user.FailedSignInCount);
        Assert.Null(user.LockedOutUntilUtc);
    }

    [Fact]
    public void TheFailureAtTheLimitLocksForTheWindowAndResetsTheCount()
    {
        User user = Users.Failed(5);

        Assert.Equal(Now + Policies.Default.Window, user.LockedOutUntilUtc);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.True(user.IsLockedOut(Now));
    }

    [Fact]
    public void AFailureWhileLockedIsIgnored()
    {
        User user = Users.Locked(Now.AddMinutes(10));

        user.RecordFailedSignIn(Now, Policies.Default);

        Assert.Equal(0, user.FailedSignInCount);
        Assert.Equal(Now.AddMinutes(10), user.LockedOutUntilUtc);
    }

    [Fact]
    public void TheLockIsNotExtendedByFurtherAttempts()
    {
        User user = Users.Failed(5);
        DateTimeOffset until = user.LockedOutUntilUtc ?? Now;

        user.RecordFailedSignIn(Now.AddMinutes(1), Policies.Default);

        Assert.Equal(until, user.LockedOutUntilUtc);
    }

    [Fact]
    public void CountingRestartsAtOneAfterTheLockExpires()
    {
        User user = Users.Locked(Now);

        user.RecordFailedSignIn(Now, Policies.Default);

        Assert.Equal(1, user.FailedSignInCount);
    }

    [Fact]
    public void APolicyOfOneLocksOnTheFirstFailure()
    {
        User user = Users.New();

        user.RecordFailedSignIn(Now, new LockoutPolicy(1, TimeSpan.FromMinutes(1)));

        Assert.Equal(Now.AddMinutes(1), user.LockedOutUntilUtc);
    }

    [Fact]
    public void RecordFailedSignInGuardsANullPolicy() =>
        NullArguments.AssertThrowsArgumentNull(Users.New().RecordFailedSignIn, Now, null);

    [Fact]
    public void ASuccessfulSignInClearsTheCounterAndTheLockAndStampsTheTime()
    {
        User user = Users.Failed(3);

        user.RecordSuccessfulSignIn(Now.AddHours(1));

        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedOutUntilUtc);
        Assert.Equal(Now.AddHours(1), user.LastSignInAtUtc);

        User locked = Users.Locked(Now.AddMinutes(10));
        locked.RecordSuccessfulSignIn(Now);
        Assert.Null(locked.LockedOutUntilUtc);
    }

    [Fact]
    public void RenameAndChangeLocaleReplaceTheValues()
    {
        User user = Users.New();

        user.Rename(Names.Of("New Name"));
        user.ChangePreferredLocale(PreferredLocale.Arabic);

        Assert.Equal("New Name", user.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, user.PreferredLocale);
    }

    [Fact]
    public void RenameAndChangeLocaleGuardNull()
    {
        User user = Users.New();

        NullArguments.AssertThrowsArgumentNull(user.Rename, new object?[] { null });
        NullArguments.AssertThrowsArgumentNull(user.ChangePreferredLocale, new object?[] { null });
    }

    [Fact]
    public void ChangePasswordSetsTheHashAndRaisesOneEventKeepingTheSession()
    {
        User user = Users.New();

        user.ChangePassword("hash-2", Ids.Session, Now);

        Assert.Equal("hash-2", user.PasswordHash);
        UserPasswordChanged raised = Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, raised.UserId);
        Assert.Equal(Ids.Session, raised.KeepSessionId);
        Assert.Equal(Now, raised.OccurredOnUtc);
    }

    [Fact]
    public void ChangePasswordWithoutASessionRaisesANullKeepSession()
    {
        User user = Users.New();

        user.ChangePassword("hash-2", null, Now);

        Assert.Null(Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents)).KeepSessionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ChangePasswordRefusesABlankHashWithoutEventOrChange(string hash)
    {
        User user = Users.New();

        Assert.Equal("Identity.PasswordHashRequired", CodeOf(() => user.ChangePassword(hash, null, Now)));

        Assert.Equal("hash-1", user.PasswordHash);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void ChangePasswordDoesNotClearALockout()
    {
        User user = Users.Locked(Now.AddMinutes(10));

        user.ChangePassword("hash-2", null, Now);

        Assert.Equal(Now.AddMinutes(10), user.LockedOutUntilUtc);
    }

    [Fact]
    public void ResetPasswordSetsTheHashClearsTheLockoutAndRaisesTheEventWithoutAKeptSession()
    {
        User user = Users.Failed(3);

        user.ResetPassword("hash-2", Now);

        Assert.Equal("hash-2", user.PasswordHash);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedOutUntilUtc);
        UserPasswordChanged raised = Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents));
        Assert.Null(raised.KeepSessionId);
    }

    [Fact]
    public void ResetPasswordClearsAnActiveLock()
    {
        User user = Users.Locked(Now.AddMinutes(10));

        user.ResetPassword("hash-2", Now);

        Assert.False(user.IsLockedOut(Now));
    }

    [Fact]
    public void ResetPasswordRefusesABlankHashBeforeAnyStateChange()
    {
        User user = Users.Failed(3);

        Assert.Equal("Identity.PasswordHashRequired", CodeOf(() => user.ResetPassword(" ", Now)));

        Assert.Equal(3, user.FailedSignInCount);
        Assert.Equal("hash-1", user.PasswordHash);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void DeactivateFlipsTheFlagAndRaisesTheEvent()
    {
        User user = Users.New();

        user.Deactivate(Now);

        Assert.False(user.IsActive);
        UserDeactivated raised = Assert.IsType<UserDeactivated>(Assert.Single(user.DomainEvents));
        Assert.Equal(user.Id, raised.UserId);
        Assert.Equal(Now, raised.OccurredOnUtc);
    }

    [Fact]
    public void DeactivatingTwiceIsRefused()
    {
        User user = Users.Inactive();

        Assert.Equal("Identity.UserAlreadyInactive", CodeOf(() => user.Deactivate(Now)));
    }

    [Fact]
    public void DeactivateKeepsTheLockout()
    {
        User user = Users.Locked(Now.AddMinutes(10));

        user.Deactivate(Now);

        Assert.Equal(Now.AddMinutes(10), user.LockedOutUntilUtc);
    }

    [Fact]
    public void ReactivateFlipsTheFlagClearsTheLockoutAndRaisesNoEvent()
    {
        User user = Users.Locked(Now.AddMinutes(10));
        user.Deactivate(Now);
        user.ClearDomainEvents();

        user.Reactivate();

        Assert.True(user.IsActive);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.Null(user.LockedOutUntilUtc);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void ReactivatingAnActiveUserIsRefused()
    {
        User user = Users.New();

        Assert.Equal("Identity.UserAlreadyActive", CodeOf(user.Reactivate));
    }
}
