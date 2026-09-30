using System.Reflection;
using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Modules.Customers.Infrastructure;

/// <summary>The reference module: copy this shape for every new module (backend/README.md, "Add a module").</summary>
public sealed class CustomersModule : IModule
{
    public string Name => "Customers";

    public Assembly ApplicationAssembly => AssemblyReference.Assembly;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CustomersDbContext, ICustomersUnitOfWork>(
            configuration, Name, CustomersDbContext.SchemaName, ApplicationAssembly);

        services.AddRepository<Customer, CustomerId, CustomersDbContext>();
        services.AddScoped<ICustomerNumberGenerator, SqlCustomerNumberGenerator>();
        services.AddScoped<IModuleSeeder, CustomersSeeder>();

        // Validation messages that are server text follow Accept-Language.
        services.AddLocalizationResource(new ResourceManager(
            "Crm.Modules.Customers.Application.Localization.Resources.CustomersMessages", ApplicationAssembly));

        // The module's public face: what other modules may reference through Crm.Modules.Customers.Contracts.
        services.AddScoped<ICustomersModuleApi, CustomersModuleApi>();
    }
}
