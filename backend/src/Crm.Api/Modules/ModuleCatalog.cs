using System.Reflection;
using Crm.BuildingBlocks.Infrastructure.Modules;

namespace Crm.Api.Modules;

/// <summary>
/// Every module the host runs - adding a module is one line here plus its two ProjectReferences in
/// Crm.Api.csproj (Infrastructure and Presentation). Nothing else in the host names a module.
/// </summary>
internal static class ModuleCatalog
{
    public static readonly IReadOnlyList<ModuleEntry> All =
    [
        // CANONICAL SLICE - removed with the Sample module in Story 11.
        new(new Crm.Modules.Sample.Infrastructure.SampleModule(), Crm.Modules.Sample.Presentation.AssemblyReference.Assembly),
    ];
}

/// <param name="Module">The module's services, DbContext, and Application assembly.</param>
/// <param name="PresentationAssembly">The module's controllers, added as an MVC application part.</param>
internal sealed record ModuleEntry(IModule Module, Assembly PresentationAssembly);
