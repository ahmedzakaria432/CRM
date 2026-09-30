using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.SharedKernel.Scoping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Modules.Organisation.Infrastructure;

public sealed class OrganisationModule : IModule
{
    public string Name => "Organisation";

    public Assembly ApplicationAssembly => AssemblyReference.Assembly;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrganisationDbContext, IOrganisationUnitOfWork>(
            configuration, Name, OrganisationDbContext.SchemaName, ApplicationAssembly);

        services.AddRepository<Domain.Organisation, TenantId, OrganisationDbContext>();
        services.AddRepository<Branch, BranchId, OrganisationDbContext>();
        services.AddRepository<Department, DepartmentId, OrganisationDbContext>();
        services.AddRepository<Membership, MembershipId, OrganisationDbContext>();

        services.AddScoped<IModuleSeeder, OrganisationSeeder>();

        // The module's public face: what other modules may reference through Crm.Modules.Organisation.Contracts.
        services.AddScoped<IOrganisationModuleApi, OrganisationModuleApi>();

        // DEVELOPMENT ONLY (Story 06): harmless outside Development - nothing calls it once DevUserAuthenticationHandler
        // returns NoResult in every other environment. Deleted with the rest of the dev-login seam by Story 07.
        services.AddScoped<IDevUserDirectory, DevUserDirectory>();
    }
}
