using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>Stands in for a module's own unit-of-work interface (<c>ICustomersUnitOfWork</c> and friends).</summary>
public interface ITestUnitOfWork : IUnitOfWork;
