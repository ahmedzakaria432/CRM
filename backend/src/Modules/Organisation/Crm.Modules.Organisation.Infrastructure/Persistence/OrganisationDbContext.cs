using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Organisation.Application;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Organisation.Infrastructure.Persistence;

public sealed class OrganisationDbContext(DbContextOptions<OrganisationDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), IOrganisationUnitOfWork
{
    public const string SchemaName = "organisation";

    protected override string Schema => SchemaName;

    public DbSet<Domain.Organisation> Organisations => Set<Domain.Organisation>();

    public DbSet<Domain.Branch> Branches => Set<Domain.Branch>();

    public DbSet<Domain.Department> Departments => Set<Domain.Department>();

    public DbSet<Domain.Membership> Memberships => Set<Domain.Membership>();
}
