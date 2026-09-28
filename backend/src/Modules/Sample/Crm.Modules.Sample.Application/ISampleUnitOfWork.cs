// CANONICAL SLICE - every module declares its own unit-of-work interface like this one.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Sample.Application;

/// <summary>The Sample module's unit of work, implemented only by SampleDbContext. Handlers in this
/// module inject this, never the bare IUnitOfWork - with one DbContext per module, the bare interface
/// would not say which module's database the save goes to.</summary>
public interface ISampleUnitOfWork : IUnitOfWork;
