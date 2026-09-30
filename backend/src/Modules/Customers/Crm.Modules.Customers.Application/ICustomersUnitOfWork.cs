using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Customers.Application;

/// <summary>The Customers module's unit of work, implemented only by CustomersDbContext. Handlers in this
/// module inject this, never the bare IUnitOfWork - with one DbContext per module, the bare interface
/// would not say which module's database the save goes to.</summary>
public interface ICustomersUnitOfWork : IUnitOfWork;
