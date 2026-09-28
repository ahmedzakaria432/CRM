using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Infrastructure.Modules;

/// <summary>
/// A module's entry point, implemented once per module in its Infrastructure project (e.g.
/// <c>SampleModule</c>) and listed in the host's <c>ModuleCatalog</c>. The host knows a module only
/// through this and through the module's Presentation assembly - never through its Domain or
/// Application types.
/// </summary>
public interface IModule
{
    /// <summary>PascalCase module name, matching the <c>Crm.Modules.&lt;Name&gt;.*</c> project names.</summary>
    string Name { get; }

    /// <summary>Scanned once by the host for MediatR handlers and FluentValidation validators.</summary>
    Assembly ApplicationAssembly { get; }

    /// <summary>Registers everything the module owns: its DbContext (via <c>AddModuleDbContext</c>),
    /// repositories, seeders, the implementations of its Contracts, and its adapters. Must not call
    /// AddMediatR - the host does that once for every module.</summary>
    void AddServices(IServiceCollection services, IConfiguration configuration);
}
