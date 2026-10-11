using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Application.Localization;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Modules.Customers.Tests.Infrastructure;

/// <summary>Inspects the registrations only: nothing resolves the DbContext or the number generator, so nothing opens SQL Server.</summary>
public sealed class CustomersModuleTests
{
    private static ServiceCollection Registered()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:Crm", "Server=unused;Database=unused;")])
            .Build();
        var services = new ServiceCollection();
        new CustomersModule().AddServices(services, configuration);
        return services;
    }

    private static ServiceDescriptor Single(ServiceCollection services, Type serviceType) =>
        Assert.Single(services, d => d.ServiceType == serviceType);

    [Fact]
    public void TheNameIsCustomers() => Assert.Equal("Customers", new CustomersModule().Name);

    [Fact]
    public void TheApplicationAssemblyIsTheModulesApplicationAssembly() =>
        Assert.Same(typeof(CustomersCacheKeys).Assembly, new CustomersModule().ApplicationAssembly);

    [Theory]
    [InlineData(typeof(ICustomersModuleApi), typeof(CustomersModuleApi))]
    [InlineData(typeof(ICustomerNumberGenerator), typeof(SqlCustomerNumberGenerator))]
    [InlineData(typeof(IModuleSeeder), typeof(CustomersSeeder))]
    public void TheServicesAreRegisteredScoped(Type service, Type implementation)
    {
        ServiceDescriptor descriptor = Single(Registered(), service);

        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheCustomerRepositoryIsScopedOverTheModuleContext()
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(IRepository<Customer, CustomerId>));

        Assert.Equal(typeof(Repository<Customer, CustomerId, CustomersDbContext>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheUnitOfWorkTheContextAndTheModuleRecordAreRegistered()
    {
        ServiceCollection services = Registered();

        Assert.Contains(services, d => d.ServiceType == typeof(CustomersDbContext));
        Assert.Equal(ServiceLifetime.Scoped, Single(services, typeof(ICustomersUnitOfWork)).Lifetime);
        Assert.Equal(
            new ModuleUnitOfWork("Customers", typeof(CustomersCacheKeys).Assembly, typeof(ICustomersUnitOfWork)),
            Single(services, typeof(ModuleUnitOfWork)).ImplementationInstance);
        Assert.Equal(
            new ModuleDatabase("Customers", "customers", typeof(CustomersDbContext)),
            Single(services, typeof(ModuleDatabase)).ImplementationInstance);
    }

    [Theory]
    [InlineData("en", "Enter the customer's name in Arabic or English.")]
    [InlineData("ar", "أدخل اسم العميل بالعربية أو الإنجليزية.")]
    public void TheLocalizationResourceResolvesTheNameRequiredMessageInBothLanguages(string culture, string expected)
    {
        LocalizationResource resource = Assert.IsType<LocalizationResource>(Single(Registered(), typeof(LocalizationResource)).ImplementationInstance);

        Assert.Equal(expected, resource.Manager.GetString(CustomersMessageKeys.Validation.NameRequired, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void AddServicesNeedsAConnectionString()
    {
        IConfiguration noConnection = new ConfigurationBuilder().Build();

        Assert.ThrowsAny<Exception>(() => new CustomersModule().AddServices(new ServiceCollection(), noConnection));
    }

    [Fact]
    public void TheModuleIsTheCustomersModuleOfTheModuleContract() =>
        Assert.IsAssignableFrom<IModule>(new CustomersModule());
}
