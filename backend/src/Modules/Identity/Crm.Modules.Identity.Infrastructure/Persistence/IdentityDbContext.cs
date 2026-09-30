using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Identity.Application;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), IIdentityUnitOfWork
{
    public const string SchemaName = "identity";

    protected override string Schema => SchemaName;

    public DbSet<Domain.User> Users => Set<Domain.User>();

    public DbSet<Domain.RefreshTokenFamily> RefreshTokenFamilies => Set<Domain.RefreshTokenFamily>();

    public DbSet<Domain.PortalAccount> PortalAccounts => Set<Domain.PortalAccount>();

    public DbSet<Domain.PortalSession> PortalSessions => Set<Domain.PortalSession>();
}
