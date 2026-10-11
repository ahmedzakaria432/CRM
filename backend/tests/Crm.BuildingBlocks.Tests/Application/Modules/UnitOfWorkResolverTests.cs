using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Tests.Application.Modules;

public sealed class UnitOfWorkResolverTests
{
    private static readonly System.Reflection.Assembly TestAssembly = typeof(ReadQuery).Assembly;

    private static ServiceProvider Provider(ITestUnitOfWork unitOfWork)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => unitOfWork);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ARequestOfARegisteredAssemblyResolvesThatModulesUnitOfWork()
    {
        var unitOfWork = Substitute.For<ITestUnitOfWork>();
        using var provider = Provider(unitOfWork);
        var resolver = new UnitOfWorkResolver([new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork))], provider);

        var resolved = resolver.ForRequest(typeof(WriteCommand));

        Assert.Same(unitOfWork, resolved);
    }

    [Fact]
    public void AnUnregisteredAssemblyFailsNamingTheTypeAndTheAssembly()
    {
        using var provider = Provider(Substitute.For<ITestUnitOfWork>());
        var resolver = new UnitOfWorkResolver([new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork))], provider);

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.ForRequest(typeof(CrmCultures)));

        Assert.Contains(typeof(CrmCultures).FullName ?? string.Empty, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Crm.BuildingBlocks.Application", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoModulesAtAllFailsLoudly()
    {
        using var provider = Provider(Substitute.For<ITestUnitOfWork>());
        var resolver = new UnitOfWorkResolver([], provider);

        Assert.Throws<InvalidOperationException>(() => resolver.ForRequest(typeof(WriteCommand)));
    }

    [Fact]
    public void TwoModulesForTheSameAssemblyIsAWiringMistake()
    {
        using var provider = Provider(Substitute.For<ITestUnitOfWork>());
        var resolver = new UnitOfWorkResolver(
            [
                new ModuleUnitOfWork("One", TestAssembly, typeof(ITestUnitOfWork)),
                new ModuleUnitOfWork("Two", TestAssembly, typeof(ITestUnitOfWork)),
            ],
            provider);

        Assert.Throws<InvalidOperationException>(() => resolver.ForRequest(typeof(WriteCommand)));
    }

    [Fact]
    public void TheUnitOfWorkFollowsTheScopeOfTheProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Substitute.For<ITestUnitOfWork>());
        using var root = services.BuildServiceProvider();
        var modules = new[] { new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork)) };

        using var scopeOne = root.CreateScope();
        using var scopeTwo = root.CreateScope();
        var inOne = new UnitOfWorkResolver(modules, scopeOne.ServiceProvider).ForRequest(typeof(WriteCommand));
        var inOneAgain = new UnitOfWorkResolver(modules, scopeOne.ServiceProvider).ForRequest(typeof(AssignCommand));
        var inTwo = new UnitOfWorkResolver(modules, scopeTwo.ServiceProvider).ForRequest(typeof(WriteCommand));

        Assert.Same(inOne, inOneAgain);
        Assert.NotSame(inOne, inTwo);
    }

    [Fact]
    public void AUnitOfWorkTypeThatWasNeverRegisteredFailsInTheContainer()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var resolver = new UnitOfWorkResolver([new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork))], provider);

        Assert.Throws<InvalidOperationException>(() => resolver.ForRequest(typeof(WriteCommand)));
    }

    [Fact]
    public void ModuleUnitOfWorkHasValueEquality()
    {
        Assert.Equal(
            new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork)),
            new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork)));
    }
}
