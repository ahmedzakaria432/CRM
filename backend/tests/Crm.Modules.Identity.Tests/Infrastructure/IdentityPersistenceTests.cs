using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts.DevSeed;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Contracts.DevSeed;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Tests.Support;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Infrastructure;

public sealed class IdentitySeederTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly RecordingLogger<IdentitySeeder> _logger = new();

    public IdentitySeederTests() => _hasher.Hash(Arg.Any<string>()).Returns(call => "hash-of-" + call.Arg<string>());

    private static IConfiguration Config(string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(password is null ? [] : [new KeyValuePair<string, string?>("Identity:DevSeed:Password", password)])
            .Build();

    private async Task Seed(string? password = "Dev-pass-123")
    {
        await using IdentityDbContext context = IdentityTestContext.Store(_database);
        await new IdentitySeeder(context, _hasher, Config(password), Clock.Fixed(), _logger).SeedAsync(Stubs.Ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingOrBlankPasswordSeedsNothingAndLogsAWarning(string? password)
    {
        await Seed(password);

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        Assert.Equal(0, await read.Users.CountAsync(Stubs.Ct));
        Assert.Equal(0, await read.PortalAccounts.CountAsync(Stubs.Ct));
        Assert.True(_logger.Contains(LogLevel.Warning, "no development users were seeded"));
    }

    [Fact]
    public async Task ItSeedsSevenUsersWithTheWellKnownIdsAndEmails()
    {
        await Seed();

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        List<User> users = await read.Users.ToListAsync(Stubs.Ct);
        Assert.Equal(7, users.Count);
        Assert.All(users, u => Assert.Equal(new TenantId(OrganisationDevSeed.TenantId), u.TenantId));
        Dictionary<Guid, string> expected = new()
        {
            [OrganisationDevSeed.AdminUserId] = IdentityDevSeed.AdminEmail,
            [OrganisationDevSeed.SupervisorUserId] = IdentityDevSeed.SupervisorEmail,
            [OrganisationDevSeed.AgentAUserId] = IdentityDevSeed.AgentAEmail,
            [OrganisationDevSeed.AgentBUserId] = IdentityDevSeed.AgentBEmail,
            [OrganisationDevSeed.AgentCUserId] = IdentityDevSeed.AgentCEmail,
            [OrganisationDevSeed.SupervisorBUserId] = IdentityDevSeed.SupervisorBEmail,
            [OrganisationDevSeed.InactiveAgentUserId] = IdentityDevSeed.InactiveAgentEmail,
        };
        Assert.All(expected, e => Assert.Equal(e.Value, users.Single(u => u.Id.Value == e.Key).Email.Value));
    }

    [Fact]
    public async Task TheArabicNamedUsersPreferArabicAndTheOthersEnglish()
    {
        await Seed();

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        List<User> users = await read.Users.ToListAsync(Stubs.Ct);
        Assert.Same(PreferredLocale.Arabic, users.Single(u => u.Id.Value == OrganisationDevSeed.SupervisorUserId).PreferredLocale);
        Assert.Same(PreferredLocale.Arabic, users.Single(u => u.Id.Value == OrganisationDevSeed.AgentBUserId).PreferredLocale);
        Assert.Same(PreferredLocale.Arabic, users.Single(u => u.Id.Value == OrganisationDevSeed.SupervisorBUserId).PreferredLocale);
        Assert.Same(PreferredLocale.English, users.Single(u => u.Id.Value == OrganisationDevSeed.AdminUserId).PreferredLocale);
        Assert.Same(PreferredLocale.English, users.Single(u => u.Id.Value == OrganisationDevSeed.AgentAUserId).PreferredLocale);
    }

    [Fact]
    public async Task OnlyTheInactiveAgentIsDeactivated()
    {
        await Seed();

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        List<User> users = await read.Users.ToListAsync(Stubs.Ct);
        User inactive = Assert.Single(users, u => !u.IsActive);
        Assert.Equal(OrganisationDevSeed.InactiveAgentUserId, inactive.Id.Value);
    }

    [Fact]
    public async Task ItSeedsFivePortalAccountsBoundToTheDevSeedCustomersAndContacts()
    {
        await Seed();

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        List<PortalAccount> accounts = await read.PortalAccounts.ToListAsync(Stubs.Ct);
        Assert.Equal(5, accounts.Count);
        PortalAccount ahmed = accounts.Single(a => a.Id.Value == IdentityDevSeed.AhmedPortalAccountId);
        Assert.Equal(CustomersDevSeed.AhmedCustomerId, ahmed.CustomerId);
        Assert.Equal(CustomersDevSeed.AhmedContactId, ahmed.ContactId);
        Assert.Equal(CustomersDevSeed.AhmedEmail, ahmed.Email.Value);
        PortalAccount huda = accounts.Single(a => a.Id.Value == IdentityDevSeed.HudaPortalAccountId);
        Assert.Equal(CustomersDevSeed.AlNoorClinicsCustomerId, huda.CustomerId);
        Assert.Equal(CustomersDevSeed.AlNoorHudaContactId, huda.ContactId);
        PortalAccount faisal = accounts.Single(a => a.Id.Value == IdentityDevSeed.FaisalPortalAccountId);
        Assert.Equal(CustomersDevSeed.FaisalContactId, faisal.ContactId);
        Assert.All(accounts, a => Assert.Equal(new TenantId(OrganisationDevSeed.TenantId), a.TenantId));
    }

    [Fact]
    public async Task EveryPasswordIsHashedThroughTheHasher()
    {
        await Seed("Dev-pass-123");

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        Assert.All(await read.Users.ToListAsync(Stubs.Ct), u => Assert.Equal("hash-of-Dev-pass-123", u.PasswordHash));
        Assert.All(await read.PortalAccounts.ToListAsync(Stubs.Ct), a => Assert.Equal("hash-of-Dev-pass-123", a.PasswordHash));
        _hasher.Received(12).Hash("Dev-pass-123");
    }

    [Fact]
    public async Task ASecondRunAddsNothingAndKeepsTheIds()
    {
        await Seed();
        await Seed();

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        Assert.Equal(7, await read.Users.CountAsync(Stubs.Ct));
        Assert.Equal(5, await read.PortalAccounts.CountAsync(Stubs.Ct));
    }

    [Fact]
    public async Task AnExistingUserIsNotOverwritten()
    {
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            store.Users.Add(User.Create(
                new TenantId(OrganisationDevSeed.TenantId), EmailAddress.Create("changed@crm.local"), DisplayName.Create("Changed Name"),
                PreferredLocale.English, "old-hash", new UserId(OrganisationDevSeed.AdminUserId)));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await Seed();

        await using IdentityDbContext read = IdentityTestContext.Store(_database);
        User admin = await read.Users.SingleAsync(u => u.Id == new UserId(OrganisationDevSeed.AdminUserId), Stubs.Ct);
        Assert.Equal("Changed Name", admin.DisplayName.Value);
        Assert.Equal("old-hash", admin.PasswordHash);
        Assert.Equal(7, await read.Users.CountAsync(Stubs.Ct));
    }
}

public sealed class IdentityDbContextTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    [Fact]
    public void TheSchemaIsIdentity() => Assert.Equal("identity", IdentityDbContext.SchemaName);

    [Fact]
    public void TheContextIsTheModulesUnitOfWork()
    {
        using IdentityDbContext context = IdentityTestContext.Store(_database);

        Assert.IsAssignableFrom<IIdentityUnitOfWork>(context);
        Assert.NotNull(context.Users);
        Assert.NotNull(context.RefreshTokenFamilies);
        Assert.NotNull(context.PortalAccounts);
        Assert.NotNull(context.PortalSessions);
    }

    [Fact]
    public void NoAggregateIsTenantScopedOrSoftDeletable()
    {
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(User)));
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(PortalAccount)));
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(RefreshTokenFamily)));
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(PortalSession)));
        Assert.False(typeof(ISoftDeletable).IsAssignableFrom(typeof(User)));
        Assert.False(typeof(ISoftDeletable).IsAssignableFrom(typeof(PortalAccount)));
        Assert.False(typeof(ISoftDeletable).IsAssignableFrom(typeof(RefreshTokenFamily)));
        Assert.False(typeof(ISoftDeletable).IsAssignableFrom(typeof(PortalSession)));
    }

    [Fact]
    public async Task ACallerWithNoScopeReadsAndWritesUsersBecauseSignInPrecedesTheScope()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Create(_database, CurrentUsers.Anonymous()))
        {
            writer.Users.Add(Users.New(Ids.TenantB));
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Create(_database, CurrentUsers.Anonymous());
        Assert.Single(await reader.Users.ToListAsync(Stubs.Ct));
    }

    [Fact]
    public async Task AnotherTenantsScopeStillReadsAndWritesAllFourAggregates()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantB)))
        {
            writer.Users.Add(Users.New(Ids.TenantA));
            writer.PortalAccounts.Add(Accounts.New(Ids.TenantA));
            writer.RefreshTokenFamilies.Add(Families.Start());
            writer.PortalSessions.Add(Sessions.Start());
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantB));
        Assert.Single(await reader.Users.ToListAsync(Stubs.Ct));
        Assert.Single(await reader.PortalAccounts.ToListAsync(Stubs.Ct));
        Assert.Single(await reader.RefreshTokenFamilies.ToListAsync(Stubs.Ct));
        Assert.Single(await reader.PortalSessions.ToListAsync(Stubs.Ct));
    }

    [Fact]
    public async Task AFamilyKeepsItsOwnedTokensAndTheRotatedFlagAfterARoundTrip()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Store(_database))
        {
            RefreshTokenFamily family = Families.Start(hash: "h1");
            family.Rotate("h1", "h2", Clock.Now.AddMinutes(1), Clock.Now.AddDays(14));
            writer.RefreshTokenFamilies.Add(family);
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Store(_database);
        RefreshTokenFamily loaded = await reader.RefreshTokenFamilies.SingleAsync(Stubs.Ct);
        Assert.Equal(2, loaded.Tokens.Count);
        RefreshToken old = loaded.Tokens.Single(t => t.TokenHash == "h1");
        RefreshToken fresh = loaded.Tokens.Single(t => t.TokenHash == "h2");
        Assert.Equal(Clock.Now, old.IssuedAtUtc);
        Assert.Equal(Clock.Now.AddDays(14), old.ExpiresAtUtc);
        Assert.Equal(Clock.Now.AddMinutes(1), old.RotatedAtUtc);
        Assert.Null(fresh.RotatedAtUtc);
    }

    [Fact]
    public async Task APortalSessionKeepsItsTokensAfterARoundTrip()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Store(_database))
        {
            writer.PortalSessions.Add(Sessions.Start(hash: "p1"));
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Store(_database);
        PortalSession loaded = await reader.PortalSessions.SingleAsync(Stubs.Ct);
        Assert.Equal("p1", Assert.Single(loaded.Tokens).TokenHash);
    }

    [Fact]
    public async Task APreferredLocaleRoundTripsAsTheSingleton()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Store(_database))
        {
            writer.Users.AddRange(
                Users.New(email: "en@example.com", locale: "en", id: UserId.New()),
                Users.New(email: "ar@example.com", locale: "ar", id: UserId.New()));
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Store(_database);
        List<User> users = await reader.Users.ToListAsync(Stubs.Ct);
        Assert.Same(PreferredLocale.English, users.Single(u => u.Email.Value == "en@example.com").PreferredLocale);
        Assert.Same(PreferredLocale.Arabic, users.Single(u => u.Email.Value == "ar@example.com").PreferredLocale);
    }

    [Fact]
    public async Task TheRevocationReasonIsStoredAndReadBack()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Store(_database))
        {
            writer.RefreshTokenFamilies.Add(Families.Revoked(SessionRevocationReason.PasswordReset));
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Store(_database);
        RefreshTokenFamily loaded = await reader.RefreshTokenFamilies.SingleAsync(Stubs.Ct);
        Assert.Equal(SessionRevocationReason.PasswordReset, loaded.RevocationReason);
        Assert.Equal(Clock.Now, loaded.RevokedAtUtc);
    }

    [Fact]
    public async Task TheAuditingInterceptorStampsCreationOnAUser()
    {
        await using (IdentityDbContext writer = IdentityTestContext.Create(_database, CurrentUsers.Admin()))
        {
            writer.Users.Add(Users.New());
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext reader = IdentityTestContext.Store(_database);
        User loaded = await reader.Users.SingleAsync(Stubs.Ct);
        Assert.Equal(Clock.Now, loaded.CreatedAtUtc);
    }
}

public sealed class RepositoryBehaviourWithIdentityAggregatesTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    [Fact]
    public async Task AnUnknownIdReturnsNull()
    {
        await using IdentityDbContext context = IdentityTestContext.Store(_database);

        Assert.Null(await IdentityTestContext.Repo<User, UserId>(context).GetByIdAsync(UserId.New(), Stubs.Ct));
        Assert.Null(await IdentityTestContext.Repo<PortalAccount, PortalAccountId>(context).GetByIdAsync(PortalAccountId.New(), Stubs.Ct));
    }

    [Fact]
    public async Task AKnownIdReturnsTheAggregate()
    {
        var id = UserId.New();
        await using (IdentityDbContext writer = IdentityTestContext.Store(_database))
        {
            writer.Users.Add(Users.New(id: id));
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);

        Assert.Equal(id, (await IdentityTestContext.Repo<User, UserId>(context).GetByIdAsync(id, Stubs.Ct))?.Id);
    }

    [Fact]
    public async Task SetOriginalRowVersionSetsTheOriginalValueOnTheTrackedUser()
    {
        var id = UserId.New();
        await using (IdentityDbContext writer = IdentityTestContext.Store(_database))
        {
            writer.Users.Add(Users.New(id: id));
            await writer.SaveChangesAsync(Stubs.Ct);
        }

        await using IdentityDbContext context = IdentityTestContext.Store(_database);
        var repository = IdentityTestContext.Repo<User, UserId>(context);
        User user = await repository.GetByIdAsync(id, Stubs.Ct) ?? throw new InvalidOperationException();

        repository.SetOriginalRowVersion(user, Ids.RowVersion());

        Assert.Equal(Ids.RowVersion(), context.Entry(user).Property(u => u.RowVersion).OriginalValue);
    }
}
