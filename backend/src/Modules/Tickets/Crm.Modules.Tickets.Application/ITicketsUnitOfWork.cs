using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Tickets.Application;

/// <summary>The Tickets module's unit of work, implemented only by TicketsDbContext. Handlers in this
/// module inject this, never the bare IUnitOfWork - with one DbContext per module, the bare interface
/// would not say which module's database the save goes to.</summary>
public interface ITicketsUnitOfWork : IUnitOfWork;
