using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Infrastructure;
using Crm.Modules.Tickets.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Modules.Tickets.Tests.Infrastructure;

/// <summary>Inspects the registrations only: nothing resolves the DbContext or the number generator, so nothing opens SQL Server.</summary>
public sealed class TicketsModuleTests
{
    private static ServiceCollection Registered()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:Crm", "Server=unused;Database=unused;")])
            .Build();
        var services = new ServiceCollection();
        new TicketsModule().AddServices(services, configuration);
        return services;
    }

    private static ServiceDescriptor Single(ServiceCollection services, Type serviceType) =>
        Assert.Single(services, d => d.ServiceType == serviceType);

    [Fact]
    public void TheNameIsTickets() => Assert.Equal("Tickets", new TicketsModule().Name);

    [Fact]
    public void TheApplicationAssemblyIsTheModulesApplicationAssembly() =>
        Assert.Same(typeof(TicketsMessageKeys).Assembly, new TicketsModule().ApplicationAssembly);

    [Theory]
    [InlineData(typeof(ITicketNumberGenerator), typeof(SqlTicketNumberGenerator))]
    [InlineData(typeof(ITicketReadModel), typeof(TicketReadModel))]
    [InlineData(typeof(ITicketThreadReadModel), typeof(TicketThreadReadModel))]
    [InlineData(typeof(IPortalTicketReadModel), typeof(PortalTicketReadModel))]
    [InlineData(typeof(IModuleSeeder), typeof(TicketsSeeder))]
    public void TheServicesAreRegisteredScoped(Type service, Type implementation)
    {
        ServiceDescriptor descriptor = Single(Registered(), service);

        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheMapperAndTheAssemblerAreRegisteredScopedAsThemselves()
    {
        ServiceCollection services = Registered();

        ServiceDescriptor mapper = Single(services, typeof(PortalTicketMapper));
        ServiceDescriptor assembler = Single(services, typeof(TicketDtoAssembler));

        Assert.Equal(ServiceLifetime.Scoped, mapper.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, assembler.Lifetime);
        Assert.Equal(typeof(PortalTicketMapper), mapper.ImplementationType);
        Assert.Equal(typeof(TicketDtoAssembler), assembler.ImplementationType);
    }

    [Fact]
    public void TheTicketAndCategoryRepositoriesAreScopedOverTheModuleContext()
    {
        ServiceCollection services = Registered();

        ServiceDescriptor tickets = Single(services, typeof(IRepository<Ticket, TicketId>));
        ServiceDescriptor categories = Single(services, typeof(IRepository<Category, CategoryId>));

        Assert.Equal(typeof(Repository<Ticket, TicketId, TicketsDbContext>), tickets.ImplementationType);
        Assert.Equal(typeof(Repository<Category, CategoryId, TicketsDbContext>), categories.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, tickets.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, categories.Lifetime);
    }

    [Fact]
    public void TheUnitOfWorkTheContextAndTheModuleRecordAreRegistered()
    {
        ServiceCollection services = Registered();

        Assert.Contains(services, d => d.ServiceType == typeof(TicketsDbContext));
        Assert.Equal(ServiceLifetime.Scoped, Single(services, typeof(ITicketsUnitOfWork)).Lifetime);
        Assert.Equal(
            new ModuleUnitOfWork("Tickets", typeof(TicketsMessageKeys).Assembly, typeof(ITicketsUnitOfWork)),
            Single(services, typeof(ModuleUnitOfWork)).ImplementationInstance);
        Assert.Equal(
            new ModuleDatabase("Tickets", "tickets", typeof(TicketsDbContext)),
            Single(services, typeof(ModuleDatabase)).ImplementationInstance);
    }

    [Theory]
    [InlineData("en", "Queue must be one of mine, unassigned, all.")]
    [InlineData("ar", "يجب أن تكون قائمة الانتظار إحدى القيم mine أو unassigned أو all.")]
    public void TheLocalizationResourceResolvesTheQueueMessageInBothLanguages(string culture, string expected)
    {
        LocalizationResource resource = Assert.IsType<LocalizationResource>(Single(Registered(), typeof(LocalizationResource)).ImplementationInstance);

        Assert.Equal(expected, resource.Manager.GetString(TicketsMessageKeys.Validation.QueueUnsupported, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void AddServicesNeedsAConnectionString()
    {
        IConfiguration noConnection = new ConfigurationBuilder().Build();

        Assert.ThrowsAny<Exception>(() => new TicketsModule().AddServices(new ServiceCollection(), noConnection));
    }

    [Fact]
    public void TheModuleIsAModuleOfTheModuleContract() => Assert.IsAssignableFrom<IModule>(new TicketsModule());

    [Fact]
    public void TheSchemaAndSequenceNamesAreTheStorageContract()
    {
        Assert.Equal("tickets", TicketsDbContext.SchemaName);
        Assert.Equal("TicketNumbers", TicketsDbContext.TicketNumberSequence);
    }
}
