using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Modules;

public sealed class ModuleServiceCollectionExtensionsTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(WriteCommand).Assembly;

    private static IConfiguration Configuration(bool withConnection = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(withConnection
                ? [new KeyValuePair<string, string?>("ConnectionStrings:Crm", "Server=localhost;Database=Crm")]
                : [])
            .Build();

    [Fact]
    public void AddRepositoryRegistersTheGenericRepositoryAsScoped()
    {
        var services = new ServiceCollection();

        services.AddRepository<DepartmentRow, Guid, TestDbContext>();

        var descriptor = Assert.Single(services);
        Assert.Equal(typeof(IRepository<DepartmentRow, Guid>), descriptor.ServiceType);
        Assert.Equal(typeof(Repository<DepartmentRow, Guid, TestDbContext>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddRepositoryReturnsTheCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddRepository<DepartmentRow, Guid, TestDbContext>());
    }

    [Fact]
    public void AddModuleDbContextRegistersTheContextAndForwardsTheUnitOfWorkToIt()
    {
        var services = new ServiceCollection();

        services.AddModuleDbContext<TestDbContext, ITestUnitOfWork>(Configuration(), "Test", "test", ApplicationAssembly);

        Assert.Contains(services, d => d.ServiceType == typeof(TestDbContext));
        var unitOfWork = Assert.Single(services, d => d.ServiceType == typeof(ITestUnitOfWork));
        Assert.Equal(ServiceLifetime.Scoped, unitOfWork.Lifetime);
        Assert.NotNull(unitOfWork.ImplementationFactory);
    }

    [Fact]
    public void AddModuleDbContextRecordsTheModuleUnitOfWorkAsASingleton()
    {
        var services = new ServiceCollection();

        services.AddModuleDbContext<TestDbContext, ITestUnitOfWork>(Configuration(), "Test", "test", ApplicationAssembly);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ModuleUnitOfWork));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(new ModuleUnitOfWork("Test", ApplicationAssembly, typeof(ITestUnitOfWork)), descriptor.ImplementationInstance);
    }

    [Fact]
    public void AddModuleDbContextRecordsTheModuleDatabaseAsASingleton()
    {
        var services = new ServiceCollection();

        services.AddModuleDbContext<TestDbContext, ITestUnitOfWork>(Configuration(), "Test", "test", ApplicationAssembly);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ModuleDatabase));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(new ModuleDatabase("Test", "test", typeof(TestDbContext)), descriptor.ImplementationInstance);
    }

    [Fact]
    public void AMissingCrmConnectionStringFailsAtRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddModuleDbContext<TestDbContext, ITestUnitOfWork>(Configuration(withConnection: false), "Test", "test", ApplicationAssembly));

        Assert.Contains("'Crm'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleDatabaseHasValueEquality()
    {
        Assert.Equal(new ModuleDatabase("A", "a", typeof(TestDbContext)), new ModuleDatabase("A", "a", typeof(TestDbContext)));
    }
}
