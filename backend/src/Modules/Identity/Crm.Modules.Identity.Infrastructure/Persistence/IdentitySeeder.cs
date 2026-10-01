using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Customers.Contracts.DevSeed;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Contracts.DevSeed;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Infrastructure.Persistence;

/// <summary>Development seed: the seven users whose memberships OrganisationSeeder created (same well-known ids) and five portal accounts (Story 59: one of them, Huda, belongs to a contact whose portal access is off, so her sign-in is refused).
/// Idempotent per well-known id. Runs only from MigrateAndSeedModulesAsync, which the host calls in Development.
/// A missing Identity:DevSeed:Password seeds nothing.</summary>
public sealed partial class IdentitySeeder(
    IdentityDbContext context,
    IPasswordHasher hasher,
    IConfiguration configuration,
    IDateTime dateTime,
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

        await EnsureAsync(tenant, OrganisationDevSeed.AdminUserId, IdentityDevSeed.AdminEmail, "System Administrator", PreferredLocale.English, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.SupervisorUserId, IdentityDevSeed.SupervisorEmail, "سارة القحطاني", PreferredLocale.Arabic, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.AgentAUserId, IdentityDevSeed.AgentAEmail, "Omar Haddad", PreferredLocale.English, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.AgentBUserId, IdentityDevSeed.AgentBEmail, "نورة الشمري", PreferredLocale.Arabic, password, deactivate: false, cancellationToken);

        await EnsureAsync(tenant, OrganisationDevSeed.AgentCUserId, IdentityDevSeed.AgentCEmail, "Hessa Al-Mutairi", PreferredLocale.English, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.SupervisorBUserId, IdentityDevSeed.SupervisorBEmail, "خالد الحربي", PreferredLocale.Arabic, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.InactiveAgentUserId, IdentityDevSeed.InactiveAgentEmail, "Majed Al-Zahrani", PreferredLocale.English, password, deactivate: true, cancellationToken);

        await EnsurePortalAccountAsync(tenant, IdentityDevSeed.AhmedPortalAccountId, CustomersDevSeed.AhmedCustomerId, CustomersDevSeed.AhmedContactId, CustomersDevSeed.AhmedEmail, "أحمد القحطاني", PreferredLocale.Arabic, password, cancellationToken);
        await EnsurePortalAccountAsync(tenant, IdentityDevSeed.MonaPortalAccountId, CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorMonaContactId, CustomersDevSeed.AlNoorMonaEmail, "منى الصالح", PreferredLocale.Arabic, password, cancellationToken);
        await EnsurePortalAccountAsync(tenant, IdentityDevSeed.YousefPortalAccountId, CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorYousefContactId, CustomersDevSeed.AlNoorYousefEmail, "Yousef Al-Amri", PreferredLocale.English, password, cancellationToken);
        await EnsurePortalAccountAsync(tenant, IdentityDevSeed.HudaPortalAccountId, CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorHudaContactId, CustomersDevSeed.AlNoorHudaEmail, "Huda Al-Shehri", PreferredLocale.English, password, cancellationToken);
        await EnsurePortalAccountAsync(tenant, IdentityDevSeed.FaisalPortalAccountId, CustomersDevSeed.FaisalCustomerId, CustomersDevSeed.FaisalContactId, CustomersDevSeed.FaisalEmail, "Faisal Al-Dosari", PreferredLocale.English, password, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Story 47 / 59: a portal account for an existing contact (a primary contact is the account admin).</summary>
    private async Task EnsurePortalAccountAsync(
        TenantId tenant, Guid accountGuid, Guid customerId, Guid contactId, string email, string displayName, PreferredLocale locale,
        string password, CancellationToken cancellationToken)
    {
        var accountId = new PortalAccountId(accountGuid);

        if (await context.PortalAccounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return;
        }

        context.PortalAccounts.Add(PortalAccount.Create(
            tenant,
            customerId,
            contactId,
            EmailAddress.Create(email),
            DisplayName.Create(displayName),
            locale,
            hasher.Hash(password),
            accountId));
    }

    private async Task EnsureAsync(
        TenantId tenant, Guid id, string email, string displayName, PreferredLocale locale, string password, bool deactivate, CancellationToken cancellationToken)
    {
        var userId = new UserId(id);

        if (await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return;
        }

        User user = User.Create(
            tenant, EmailAddress.Create(email), DisplayName.Create(displayName), locale, hasher.Hash(password), userId);
        if (deactivate)
        {
            user.Deactivate(dateTime.UtcNow);
        }

        context.Users.Add(user);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity:DevSeed:Password is not configured; no development users were seeded")]
    private partial void LogNoPassword();
}
