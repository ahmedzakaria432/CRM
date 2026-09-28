using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Application.Modules;

public sealed class UnitOfWorkResolver(IEnumerable<ModuleUnitOfWork> modules, IServiceProvider serviceProvider)
    : IUnitOfWorkResolver
{
    public IUnitOfWork ForRequest(Type requestType)
    {
        ModuleUnitOfWork module = modules.SingleOrDefault(m => m.ApplicationAssembly == requestType.Assembly)
            ?? throw new InvalidOperationException(
                $"'{requestType.FullName}' is a command, but no module registered a unit of work for " +
                $"'{requestType.Assembly.GetName().Name}'. Call AddModuleDbContext in the module's Infrastructure registration.");

        return (IUnitOfWork)serviceProvider.GetRequiredService(module.UnitOfWorkType);
    }
}
