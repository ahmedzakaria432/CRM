using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Domain;

public sealed class PortalAccountTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Error.Code;

    private static PortalAccount Locked(DateTimeOffset until)
    {
        PortalAccount account = Accounts.New();
        DateTimeOffset at = until - Policies.Default.Window;
        for (int i = 0; i < Policies.Default.MaxFailedAttempts; i++)
        {
            account.RecordFailedSignIn(at, Policies.Default);
        }

        return account;
    }

    [Fact]
    public void CreateStartsWithZeroCountersAndNoEvent()
    {
        PortalAccount account = Accounts.New(Ids.TenantB, Ids.OtherCustomer, Ids.OtherContact, "x@y.com", "Name", "ar", "hash-x");

        Assert.Equal(Ids.TenantB, account.TenantId);
        Assert.Equal(Ids.OtherCustomer, account.CustomerId);
        Assert.Equal(Ids.OtherContact, account.ContactId);
        Assert.Equal("x@y.com", account.Email.Value);
        Assert.Equal("Name", account.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, account.PreferredLocale);
        Assert.Equal("hash-x", account.PasswordHash);
        Assert.Equal(0, account.FailedSignInCount);
        Assert.Null(account.LockedOutUntilUtc);
        Assert.Null(account.LastSignInAtUtc);
        Assert.Empty(account.RowVersion);
        Assert.Empty(account.DomainEvents);
    }

    [Fact]
    public void CreateUsesTheSuppliedIdOrGeneratesOne()
    {
        var id = PortalAccountId.New();

        Assert.Equal(id, Accounts.New(id: id).Id);
        Assert.NotEqual(Guid.Empty, PortalAccount.Create(Ids.TenantA, Ids.Customer, Ids.Contact, Emails.Of(), Names.Of(), PreferredLocale.English, "h").Id.Value);
    }

    [Fact]
    public void CreateRefusesTheNoneTenant() =>
        Assert.Equal("Identity.TenantRequired", CodeOf(() => Accounts.New(TenantId.None)));

    [Fact]
    public void CreateRefusesAnEmptyCustomer() =>
        Assert.Equal("Identity.PortalContactRequired", CodeOf(() => Accounts.New(customerId: Guid.Empty)));

    [Fact]
    public void CreateRefusesAnEmptyContact() =>
        Assert.Equal("Identity.PortalContactRequired", CodeOf(() => Accounts.New(contactId: Guid.Empty)));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void CreateRefusesABlankHash(string hash) =>
        Assert.Equal("Identity.PasswordHashRequired", CodeOf(() => Accounts.New(hash: hash)));

    [Fact]
    public void CreateGuardsNullArguments()
    {
        NullArguments.AssertThrowsArgumentNull(PortalAccount.Create, null, Ids.Customer, Ids.Contact, Emails.Of(), Names.Of(), PreferredLocale.English, "h", null);
        NullArguments.AssertThrowsArgumentNull(PortalAccount.Create, Ids.TenantA, Ids.Customer, Ids.Contact, null, Names.Of(), PreferredLocale.English, "h", null);
        NullArguments.AssertThrowsArgumentNull(PortalAccount.Create, Ids.TenantA, Ids.Customer, Ids.Contact, Emails.Of(), null, PreferredLocale.English, "h", null);
        NullArguments.AssertThrowsArgumentNull(PortalAccount.Create, Ids.TenantA, Ids.Customer, Ids.Contact, Emails.Of(), Names.Of(), null, "h", null);
    }

    [Fact]
    public void TheTenantIsCheckedBeforeTheContactAndTheContactBeforeTheHash()
    {
        Assert.Equal("Identity.TenantRequired", CodeOf(() => Accounts.New(TenantId.None, contactId: Guid.Empty, hash: "")));
        Assert.Equal("Identity.PortalContactRequired", CodeOf(() => Accounts.New(contactId: Guid.Empty, hash: "")));
    }

    [Fact]
    public void IsLockedOutFollowsTheLockInstant()
    {
        Assert.False(Accounts.New().IsLockedOut(Now));
        Assert.True(Locked(Now.AddTicks(1)).IsLockedOut(Now));
        Assert.False(Locked(Now).IsLockedOut(Now));
    }

    [Fact]
    public void FailedSignInIncrementsBelowTheLimit()
    {
        PortalAccount account = Accounts.Failed(4);

        Assert.Equal(4, account.FailedSignInCount);
        Assert.Null(account.LockedOutUntilUtc);
    }

    [Fact]
    public void TheFailureAtTheLimitLocksAndResetsTheCount()
    {
        PortalAccount account = Accounts.Failed(5);

        Assert.Equal(Now + Policies.Default.Window, account.LockedOutUntilUtc);
        Assert.Equal(0, account.FailedSignInCount);
    }

    [Fact]
    public void AFailureWhileLockedIsIgnored()
    {
        PortalAccount account = Locked(Now.AddMinutes(10));

        account.RecordFailedSignIn(Now, Policies.Default);

        Assert.Equal(0, account.FailedSignInCount);
        Assert.Equal(Now.AddMinutes(10), account.LockedOutUntilUtc);
    }

    [Fact]
    public void CountingRestartsWhenTheLockExpiresExactlyNow()
    {
        PortalAccount account = Locked(Now);

        account.RecordFailedSignIn(Now, Policies.Default);

        Assert.Equal(1, account.FailedSignInCount);
    }

    [Fact]
    public void RecordFailedSignInGuardsANullPolicy() =>
        NullArguments.AssertThrowsArgumentNull(Accounts.New().RecordFailedSignIn, Now, null);

    [Fact]
    public void ASuccessfulSignInClearsTheCounterAndTheLockAndStampsTheTime()
    {
        PortalAccount account = Locked(Now.AddMinutes(10));

        account.RecordSuccessfulSignIn(Now);

        Assert.Equal(0, account.FailedSignInCount);
        Assert.Null(account.LockedOutUntilUtc);
        Assert.Equal(Now, account.LastSignInAtUtc);
    }

    [Fact]
    public void UpdateProfileReplacesBothValues()
    {
        PortalAccount account = Accounts.New();

        account.UpdateProfile(Names.Of("New"), PreferredLocale.Arabic);

        Assert.Equal("New", account.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, account.PreferredLocale);
    }

    [Fact]
    public void UpdateProfileGuardsNullArguments()
    {
        PortalAccount account = Accounts.New();

        NullArguments.AssertThrowsArgumentNull(account.UpdateProfile, null, PreferredLocale.English);
        NullArguments.AssertThrowsArgumentNull(account.UpdateProfile, Names.Of(), null);
    }

    [Fact]
    public void ChangePasswordRaisesTheEventWithTheKeptSession()
    {
        PortalAccount account = Accounts.New();

        account.ChangePassword("hash-2", Ids.Session, Now);

        Assert.Equal("hash-2", account.PasswordHash);
        PortalPasswordChanged raised = Assert.IsType<PortalPasswordChanged>(Assert.Single(account.DomainEvents));
        Assert.Equal(account.Id, raised.AccountId);
        Assert.Equal(Ids.Session, raised.KeepSessionId);
        Assert.Equal(Now, raised.OccurredOnUtc);
    }

    [Fact]
    public void ChangePasswordRefusesABlankHashWithoutEventOrChange()
    {
        PortalAccount account = Accounts.New();

        Assert.Equal("Identity.PasswordHashRequired", CodeOf(() => account.ChangePassword(" ", null, Now)));

        Assert.Equal("hash-1", account.PasswordHash);
        Assert.Empty(account.DomainEvents);
    }

    [Fact]
    public void SetPasswordByAgentClearsTheLockoutAndRaisesTheEventWithoutAKeptSession()
    {
        PortalAccount account = Locked(Now.AddMinutes(10));

        account.SetPasswordByAgent("hash-2", Now);

        Assert.Equal("hash-2", account.PasswordHash);
        Assert.Null(account.LockedOutUntilUtc);
        Assert.Equal(0, account.FailedSignInCount);
        Assert.Null(Assert.IsType<PortalPasswordChanged>(Assert.Single(account.DomainEvents)).KeepSessionId);
    }

    [Fact]
    public void SetPasswordByAgentRefusesABlankHashBeforeAnyStateChange()
    {
        PortalAccount account = Accounts.Failed(3);

        Assert.Equal("Identity.PasswordHashRequired", CodeOf(() => account.SetPasswordByAgent("", Now)));

        Assert.Equal(3, account.FailedSignInCount);
        Assert.Empty(account.DomainEvents);
    }
}
