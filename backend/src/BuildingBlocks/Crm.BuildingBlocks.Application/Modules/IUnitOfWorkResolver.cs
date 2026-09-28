using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Application.Modules;

public interface IUnitOfWorkResolver
{
    /// <summary>The unit of work of the module whose Application assembly declares
    /// <paramref name="requestType"/>. Throws when no module registered one - a command in a module
    /// with no DbContext is a wiring mistake, and failing loudly beats running it untransacted.</summary>
    IUnitOfWork ForRequest(Type requestType);
}
