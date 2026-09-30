using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Contracts.DevSeed;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Infrastructure.Persistence;

/// <summary>Development seed: the four users whose memberships OrganisationSeeder created (same well-known ids).
/// Idempotent per well-known id. Runs only from MigrateAndSeedModulesAsync, which the host calls in Development.
/// A missing Identity:DevSeed:Password seeds nothing.</summary>
public sealed partial class IdentitySeeder(
    IdentityDbContext context,
    IPasswordHasher hasher,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        string? password = configuration["Identity:DevSeed:Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            LogNoPassword();
            return;
        }

        var tenant = new TenantId(OrganisationDevSeed.TenantId);

        await EnsureAsync(tenant, OrganisationDevSeed.AdminUserId, IdentityDevSeed.AdminEmail, "System Administrator", PreferredLocale.English, password, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.SupervisorUserId, IdentityDevSeed.SupervisorEmail, "سارة القحطاني", PreferredLocale.Arabic, password, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.AgentAUserId, IdentityDevSeed.AgentAEmail, "Omar Haddad", PreferredLocale.English, password, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.AgentBUserId, IdentityDevSeed.AgentBEmail, "نورة الشمري", PreferredLocale.Arabic, password, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAsync(
        TenantId tenant, Guid id, string email, string displayName, PreferredLocale locale, string password, CancellationToken cancellationToken)
    {
        var userId = new UserId(id);

        if (await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return;
        }

        context.Users.Add(User.Create(
            tenant, EmailAddress.Create(email), DisplayName.Create(displayName), locale, hasher.Hash(password), userId));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity:DevSeed:Password is not configured; no development users were seeded")]
    private partial void LogNoPassword();
}
