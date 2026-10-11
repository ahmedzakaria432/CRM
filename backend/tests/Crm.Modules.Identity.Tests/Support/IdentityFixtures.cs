using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Support;

public static class Emails
{
    public static EmailAddress Of(string value = "agent@example.com") => EmailAddress.Create(value);
}

public static class Names
{
    public static DisplayName Of(string value = "Omar Haddad") => DisplayName.Create(value);
}

public static class Policies
{
    public static LockoutPolicy Default => new(5, TimeSpan.FromMinutes(15));
}

public static class Settings
{
    public static SessionSettings Default => new(TimeSpan.FromDays(14), 5, TimeSpan.FromMinutes(15));
}

public static class Users
{
    public static User New(
        TenantId? tenant = null,
        string email = "agent@example.com",
        string name = "Omar Haddad",
        string locale = "en",
        string hash = "hash-1",
        UserId? id = null) =>
        User.Create(tenant ?? Ids.TenantA, Emails.Of(email), Names.Of(name), PreferredLocale.Create(locale), hash, id ?? new UserId(Ids.User));

    public static User Inactive(TenantId? tenant = null)
    {
        User user = New(tenant);
        user.Deactivate(Clock.Now);
        user.ClearDomainEvents();
        return user;
    }

    /// <summary>A user whose lock runs until the given instant (reached through the failed-sign-in counter).</summary>
    public static User Locked(DateTimeOffset until, TenantId? tenant = null)
    {
        User user = New(tenant);
        DateTimeOffset at = until - Policies.Default.Window;
        for (int i = 0; i < Policies.Default.MaxFailedAttempts; i++)
        {
            user.RecordFailedSignIn(at, Policies.Default);
        }

        return user;
    }

    public static User Failed(int attempts, TenantId? tenant = null)
    {
        User user = New(tenant);
        for (int i = 0; i < attempts; i++)
        {
            user.RecordFailedSignIn(Clock.Now, Policies.Default);
        }

        return user;
    }
}

public static class Accounts
{
    public static PortalAccount New(
        TenantId? tenant = null,
        Guid? customerId = null,
        Guid? contactId = null,
        string email = "customer@example.com",
        string name = "Layla Saleh",
        string locale = "en",
        string hash = "hash-1",
        PortalAccountId? id = null) =>
        PortalAccount.Create(
            tenant ?? Ids.TenantA, customerId ?? Ids.Customer, contactId ?? Ids.Contact, Emails.Of(email), Names.Of(name),
            PreferredLocale.Create(locale), hash, id ?? new PortalAccountId(Ids.Account));

    public static PortalAccount Failed(int attempts)
    {
        PortalAccount account = New();
        for (int i = 0; i < attempts; i++)
        {
            account.RecordFailedSignIn(Clock.Now, Policies.Default);
        }

        return account;
    }
}

public static class Families
{
    public static RefreshTokenFamily Start(UserId? userId = null, string hash = "token-hash-1", DateTimeOffset? now = null, DateTimeOffset? expires = null, RefreshTokenFamilyId? id = null)
    {
        DateTimeOffset at = now ?? Clock.Now;
        RefreshTokenFamily family = RefreshTokenFamily.Start(
            userId ?? new UserId(Ids.User), hash, at, expires ?? at.AddDays(14), id ?? new RefreshTokenFamilyId(Ids.Family));
        family.ClearDomainEvents();
        return family;
    }

    public static RefreshTokenFamily Revoked(SessionRevocationReason reason = SessionRevocationReason.SignedOut)
    {
        RefreshTokenFamily family = Start();
        family.Revoke(reason, Clock.Now);
        return family;
    }
}

public static class Sessions
{
    public static PortalSession Start(PortalAccountId? accountId = null, string hash = "token-hash-1", DateTimeOffset? now = null, DateTimeOffset? expires = null, PortalSessionId? id = null)
    {
        DateTimeOffset at = now ?? Clock.Now;
        PortalSession session = PortalSession.Start(
            accountId ?? new PortalAccountId(Ids.Account), hash, at, expires ?? at.AddDays(14), id ?? new PortalSessionId(Ids.Family));
        session.ClearDomainEvents();
        return session;
    }

    public static PortalSession Revoked(SessionRevocationReason reason = SessionRevocationReason.SignedOut)
    {
        PortalSession session = Start();
        session.Revoke(reason, Clock.Now);
        return session;
    }
}

public static class CustomerFixtures
{
    public static CustomerContactSummary Contact(Guid? id = null, string name = "Layla Saleh", string? email = "customer@example.com", bool isPrimary = true, bool hasPortalAccess = true) =>
        new(id ?? Ids.Contact, name, email, null, isPrimary, hasPortalAccess);

    public static CustomerSummary Summary(bool isActive = true, string language = "en", TenantId? tenant = null, params CustomerContactSummary[] contacts) =>
        new(
            Ids.Customer, "C-0001", "Customer ar", "Customer en", "Individual", isActive, language,
            (tenant ?? Ids.TenantA).Value, Ids.BranchA1.Value, Ids.DeptA1.Value, contacts);

    public static PortalContactMatch Match(
        Guid? customerId = null,
        Guid? contactId = null,
        string name = "Layla Saleh",
        string email = "customer@example.com",
        bool isPrimary = true,
        string language = "en",
        TenantId? tenant = null) =>
        new(
            customerId ?? Ids.Customer, contactId ?? Ids.Contact, name, email, isPrimary, language,
            (tenant ?? Ids.TenantA).Value, Ids.BranchA1.Value, Ids.DeptA1.Value);
}

public static class Memberships
{
    public static UserMemberships Of(Guid userId, TenantId tenant, params (DepartmentId Department, BranchId Branch, string Role)[] memberships) =>
        new(userId, tenant.Value, [.. memberships.Select(m => new UserMembership(Guid.NewGuid(), m.Department.Value, m.Branch.Value, m.Role))]);
}

public static class Outcome
{
    public static void Succeeded(Result<SignInOutcome> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Succeeded);
        Assert.NotNull(result.Value.Tokens);
        Assert.Null(result.Value.FailureCode);
    }

    public static void Failed(Result<SignInOutcome> result, string code)
    {
        ArgumentNullException.ThrowIfNull(result);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Succeeded);
        Assert.Null(result.Value.Tokens);
        Assert.Equal(code, result.Value.FailureCode);
    }
}

public static class Scope
{
    public static OrganisationScope OfTenantA() => Scopes.AgentOfTenantA(Scopes.Member(Ids.DeptA1, Ids.BranchA1));
}
