using System.Reflection;

namespace Crm.BuildingBlocks.Application.Modules;

/// <summary>
/// Binds a module's Application assembly to that module's unit-of-work interface (e.g.
/// <c>ISampleUnitOfWork</c>). Every module owns its own DbContext, so there is no single
/// <see cref="Abstractions.IUnitOfWork"/> in the container: TransactionBehavior uses this registration to
/// open the transaction on the DbContext of the module the command belongs to. Registered by
/// <c>AddModuleDbContext</c> in Crm.BuildingBlocks.Infrastructure - a module never registers it by hand.
/// </summary>
public sealed record ModuleUnitOfWork(string ModuleName, Assembly ApplicationAssembly, Type UnitOfWorkType);
