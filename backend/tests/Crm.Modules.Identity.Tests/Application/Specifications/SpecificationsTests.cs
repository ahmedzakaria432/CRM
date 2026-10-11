using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Application.Specifications;

public sealed class UserByEmailSpecificationTests
{
    [Fact]
    public async Task MatchesTheNormalisedEmailAndNothingElse()
    {
        string database = IdentityTestContext.NewDatabaseName();
        await using (IdentityDbContext store = IdentityTestContext.Store(database))
        {
            store.Users.AddRange(
                Users.New(email: "a@b.com", id: UserId.New()),
                Users.New(email: "c@b.com", id: UserId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(database);
        var repository = IdentityTestContext.Repo<User, UserId>(context);

        User? found = await repository.FirstOrDefaultAsync(new UserByEmailSpecification(Emails.Of(" A@B.COM ")), Stubs.Ct);

        Assert.Equal("a@b.com", found?.Email.Value);
        Assert.Equal(1, await repository.CountAsync(new UserByEmailSpecification(Emails.Of("a@b.com")), Stubs.Ct));
    }

    [Fact]
    public async Task EmailUniquenessIsGlobalSoAnotherTenantsUserMatchesToo()
    {
        string database = IdentityTestContext.NewDatabaseName();
        await using (IdentityDbContext store = IdentityTestContext.Store(database))
        {
            store.Users.Add(Users.New(Ids.TenantB, "a@b.com", id: UserId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Create(database, CurrentUsers.Admin());

        Assert.Equal(1, await IdentityTestContext.Repo<User, UserId>(context).CountAsync(new UserByEmailSpecification(Emails.Of("a@b.com")), Stubs.Ct));
    }

    [Fact]
    public void IsTrackedAndGuardsANullEmail()
    {
        Assert.False(new UserByEmailSpecification(Emails.Of()).AsNoTracking);
        NullArguments.AssertThrowsArgumentNull((Func<EmailAddress, UserByEmailSpecification>)(e => new UserByEmailSpecification(e)), new object?[] { null });
    }
}

public sealed class UsersSpecificationTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    private async Task SeedAsync()
    {
        await using IdentityDbContext store = IdentityTestContext.Store(_database);
        store.Users.AddRange(
            Users.New(email: "amal@x.com", name: "Amal", id: UserId.New()),
            Users.New(email: "bilal@y.com", name: "Bilal", id: UserId.New()),
            Users.New(email: "carim@x.com", name: "Carim", id: UserId.New()),
            Users.New(Ids.TenantB, "other@x.com", "Aaron", id: UserId.New()));
        await store.SaveChangesAsync(Stubs.Ct);
    }

    private async Task<IReadOnlyList<string>> Names(UsersSpecification specification)
    {
        await using IdentityDbContext context = IdentityTestContext.Create(_database, CurrentUsers.Admin());

        return [.. (await IdentityTestContext.Repo<User, UserId>(context).ListAsync(specification, Stubs.Ct)).Select(u => u.DisplayName.Value)];
    }

    [Fact]
    public async Task TheTenantFilterExcludesOtherTenants()
    {
        await SeedAsync();

        Assert.Equal(["Amal", "Bilal", "Carim"], await Names(new UsersSpecification(Ids.TenantA, null, null, null)));
        Assert.Equal(["Aaron"], await Names(new UsersSpecification(Ids.TenantB, null, null, null)));
        Assert.Empty(await Names(new UsersSpecification(TenantId.None, null, null, null)));
    }

    [Fact]
    public async Task SearchMatchesEmailOrDisplayNameWithTheSameCaseAndTrims()
    {
        await SeedAsync();

        Assert.Equal(["Amal", "Carim"], await Names(new UsersSpecification(Ids.TenantA, "x.com", null, null)));
        Assert.Equal(["Bilal"], await Names(new UsersSpecification(Ids.TenantA, "  Bilal  ", null, null)));
    }

    [Fact]
    public async Task ASearchIsCaseSensitiveOnInMemoryAndLeansOnTheSqlServerCollationInProduction()
    {
        await SeedAsync();

        Assert.Empty(await Names(new UsersSpecification(Ids.TenantA, "AMAL", null, null)));
    }

    [Fact]
    public async Task ResultsAreOrderedByDisplayName()
    {
        await SeedAsync();

        Assert.Equal(["Amal", "Bilal", "Carim"], await Names(new UsersSpecification(Ids.TenantA, null, null, null)));
    }

    [Fact]
    public async Task PagingAppliesWhenBothArgumentsAreGiven()
    {
        await SeedAsync();

        Assert.Equal(["Carim"], await Names(new UsersSpecification(Ids.TenantA, null, 2, 2)));
    }

    [Fact]
    public void PagingIsSkippedUnlessBothArgumentsAreGiven()
    {
        Assert.Null(new UsersSpecification(Ids.TenantA, null, 2, null).Take);
        Assert.Null(new UsersSpecification(Ids.TenantA, null, null, 2).Skip);
        Assert.Null(new UsersSpecification(Ids.TenantA, null, null, null).Skip);
        var paged = new UsersSpecification(Ids.TenantA, null, 3, 10);
        Assert.Equal(20, paged.Skip);
        Assert.Equal(10, paged.Take);
    }

    [Fact]
    public void ItIsReadOnlyAndGuardsANullTenant()
    {
        Assert.True(new UsersSpecification(Ids.TenantA, null, null, null).AsNoTracking);
        NullArguments.AssertThrowsArgumentNull((Func<TenantId, UsersSpecification>)(t => new UsersSpecification(t, null, null, null)), new object?[] { null });
    }
}

public sealed class UsersByIdsSpecificationTests
{
    [Fact]
    public async Task MatchesTheTenantAndTheIdsOnly()
    {
        string database = IdentityTestContext.NewDatabaseName();
        var wanted = UserId.New();
        var unwanted = UserId.New();
        var foreign = UserId.New();
        await using (IdentityDbContext store = IdentityTestContext.Store(database))
        {
            store.Users.AddRange(
                Users.New(email: "a@x.com", id: wanted),
                Users.New(email: "b@x.com", id: unwanted),
                Users.New(Ids.TenantB, "c@x.com", id: foreign));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(database);
        var repository = IdentityTestContext.Repo<User, UserId>(context);

        IReadOnlyList<User> found = await repository.ListAsync(new UsersByIdsSpecification(Ids.TenantA, [wanted, foreign]), Stubs.Ct);
        IReadOnlyList<User> none = await repository.ListAsync(new UsersByIdsSpecification(Ids.TenantA, []), Stubs.Ct);

        Assert.Equal(wanted, Assert.Single(found).Id);
        Assert.Empty(none);
    }

    [Fact]
    public void ItIsReadOnlyAndGuardsNulls()
    {
        Assert.True(new UsersByIdsSpecification(Ids.TenantA, []).AsNoTracking);
        NullArguments.AssertThrowsArgumentNull((Func<TenantId, IReadOnlyList<UserId>, UsersByIdsSpecification>)((t, i) => new UsersByIdsSpecification(t, i)), null, new List<UserId>());
        NullArguments.AssertThrowsArgumentNull((Func<TenantId, IReadOnlyList<UserId>, UsersByIdsSpecification>)((t, i) => new UsersByIdsSpecification(t, i)), Ids.TenantA, null);
    }
}

public sealed class RefreshTokenFamilySpecificationsTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    [Fact]
    public async Task TheHashSpecificationFindsAFamilyByAnyTokenOfTheChain()
    {
        var chained = new RefreshTokenFamilyId(Ids.Family);
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            RefreshTokenFamily family = Families.Start(hash: "old", id: chained);
            family.Rotate("old", "new", Clock.Now, Clock.Now.AddDays(1));
            store.RefreshTokenFamilies.AddRange(family, Families.Start(hash: "unrelated", id: RefreshTokenFamilyId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);
        var repository = IdentityTestContext.Repo<RefreshTokenFamily, RefreshTokenFamilyId>(context);

        Assert.Equal(chained, (await repository.FirstOrDefaultAsync(new RefreshTokenFamilyByTokenHashSpecification("old"), Stubs.Ct))?.Id);
        Assert.Equal(chained, (await repository.FirstOrDefaultAsync(new RefreshTokenFamilyByTokenHashSpecification("new"), Stubs.Ct))?.Id);
        Assert.Null(await repository.FirstOrDefaultAsync(new RefreshTokenFamilyByTokenHashSpecification(chained.Value.ToString()), Stubs.Ct));
        Assert.Null(await repository.FirstOrDefaultAsync(new RefreshTokenFamilyByTokenHashSpecification("missing"), Stubs.Ct));
    }

    [Fact]
    public async Task TheActiveSpecificationExcludesRevokedAndOtherUsersFamilies()
    {
        var mine = RefreshTokenFamilyId.New();
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            store.RefreshTokenFamilies.AddRange(
                Families.Start(hash: "h1", id: mine),
                Families.Revoked(),
                Families.Start(new UserId(Ids.OtherUser), "h3", id: RefreshTokenFamilyId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);

        IReadOnlyList<RefreshTokenFamily> active = await IdentityTestContext.Repo<RefreshTokenFamily, RefreshTokenFamilyId>(context)
            .ListAsync(new ActiveRefreshTokenFamiliesOfUserSpecification(new UserId(Ids.User)), Stubs.Ct);

        Assert.Equal(mine, Assert.Single(active).Id);
    }

    [Fact]
    public void BothAreTrackedAndTheActiveOneGuardsANullUser()
    {
        Assert.False(new RefreshTokenFamilyByTokenHashSpecification("h").AsNoTracking);
        Assert.False(new ActiveRefreshTokenFamiliesOfUserSpecification(new UserId(Ids.User)).AsNoTracking);
        NullArguments.AssertThrowsArgumentNull((Func<UserId, ActiveRefreshTokenFamiliesOfUserSpecification>)(u => new ActiveRefreshTokenFamiliesOfUserSpecification(u)), new object?[] { null });
    }
}

public sealed class PortalSessionSpecificationsTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    [Fact]
    public async Task TheHashSpecificationFindsASessionByAnyTokenOfTheChain()
    {
        var chained = new PortalSessionId(Ids.Session);
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            PortalSession session = Sessions.Start(hash: "old", id: chained);
            session.Rotate("old", "new", Clock.Now, Clock.Now.AddDays(1));
            store.PortalSessions.AddRange(session, Sessions.Start(hash: "unrelated", id: PortalSessionId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);
        var repository = IdentityTestContext.Repo<PortalSession, PortalSessionId>(context);

        Assert.Equal(chained, (await repository.FirstOrDefaultAsync(new PortalSessionByTokenHashSpecification("old"), Stubs.Ct))?.Id);
        Assert.Equal(chained, (await repository.FirstOrDefaultAsync(new PortalSessionByTokenHashSpecification("new"), Stubs.Ct))?.Id);
        Assert.Null(await repository.FirstOrDefaultAsync(new PortalSessionByTokenHashSpecification("missing"), Stubs.Ct));
    }

    [Fact]
    public async Task TheActiveSpecificationExcludesRevokedAndOtherAccountsSessions()
    {
        var mine = PortalSessionId.New();
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            store.PortalSessions.AddRange(
                Sessions.Start(hash: "h1", id: mine),
                Sessions.Revoked(),
                Sessions.Start(PortalAccountId.New(), "h3", id: PortalSessionId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);

        IReadOnlyList<PortalSession> active = await IdentityTestContext.Repo<PortalSession, PortalSessionId>(context)
            .ListAsync(new ActivePortalSessionsOfAccountSpecification(new PortalAccountId(Ids.Account)), Stubs.Ct);

        Assert.Equal(mine, Assert.Single(active).Id);
    }

    [Fact]
    public void BothAreTrackedAndTheActiveOneGuardsANullAccount()
    {
        Assert.False(new PortalSessionByTokenHashSpecification("h").AsNoTracking);
        Assert.False(new ActivePortalSessionsOfAccountSpecification(new PortalAccountId(Ids.Account)).AsNoTracking);
        NullArguments.AssertThrowsArgumentNull((Func<PortalAccountId, ActivePortalSessionsOfAccountSpecification>)(a => new ActivePortalSessionsOfAccountSpecification(a)), new object?[] { null });
    }
}

public sealed class PortalAccountSpecificationsTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    [Fact]
    public async Task TheEmailSpecificationMatchesTheNormalisedEmailAcrossTenants()
    {
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            store.PortalAccounts.AddRange(
                Accounts.New(Ids.TenantB, email: "a@b.com", id: PortalAccountId.New()),
                Accounts.New(email: "c@b.com", contactId: Ids.OtherContact, id: PortalAccountId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Create(_database, CurrentUsers.Admin());
        var repository = IdentityTestContext.Repo<PortalAccount, PortalAccountId>(context);

        IReadOnlyList<PortalAccount> found = await repository.ListAsync(new PortalAccountByEmailSpecification(Emails.Of(" A@B.COM ")), Stubs.Ct);

        Assert.Equal("a@b.com", Assert.Single(found).Email.Value);
    }

    [Fact]
    public async Task TheContactSpecificationMatchesTheContactOnly()
    {
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            store.PortalAccounts.AddRange(
                Accounts.New(email: "a@b.com", id: PortalAccountId.New()),
                Accounts.New(email: "c@b.com", contactId: Ids.OtherContact, id: PortalAccountId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);

        IReadOnlyList<PortalAccount> found = await IdentityTestContext.Repo<PortalAccount, PortalAccountId>(context)
            .ListAsync(new PortalAccountByContactSpecification(Ids.OtherContact), Stubs.Ct);

        Assert.Equal("c@b.com", Assert.Single(found).Email.Value);
    }

    [Fact]
    public void BothAreTrackedAndTheEmailOneGuardsANullEmail()
    {
        Assert.False(new PortalAccountByEmailSpecification(Emails.Of()).AsNoTracking);
        Assert.False(new PortalAccountByContactSpecification(Ids.Contact).AsNoTracking);
        NullArguments.AssertThrowsArgumentNull((Func<EmailAddress, PortalAccountByEmailSpecification>)(e => new PortalAccountByEmailSpecification(e)), new object?[] { null });
    }
}
