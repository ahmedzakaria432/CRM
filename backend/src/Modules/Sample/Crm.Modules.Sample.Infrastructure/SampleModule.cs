// CANONICAL SLICE - every module has exactly one IModule shaped like this one.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using System.Reflection;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Sample.Application;
using Crm.Modules.Sample.Contracts;
using Crm.Modules.Sample.Domain;
using Crm.Modules.Sample.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Modules.Sample.Infrastructure;

public sealed class SampleModule : IModule
{
    public string Name => "Sample";

    public Assembly ApplicationAssembly => AssemblyReference.Assembly;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SampleDbContext, ISampleUnitOfWork>(
            configuration, Name, SampleDbContext.SchemaName, ApplicationAssembly);

        services.AddRepository<SampleAggregate, SampleId, SampleDbContext>();
        services.AddScoped<IModuleSeeder, SampleSeeder>();

        // The module's public face: what other modules may reference through Crm.Modules.Sample.Contracts.
        services.AddScoped<ISampleModuleApi, SampleModuleApi>();
    }
}
