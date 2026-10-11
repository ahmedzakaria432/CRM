using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Localization;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Infrastructure.Theming;
using Crm.SharedKernel.Scoping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Infrastructure;

/// <summary>Inspects the registrations only: nothing resolves the DbContext, so nothing opens SQL Server.</summary>
public sealed class OrganisationModuleTests
{
    private static ServiceCollection Registered()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:Crm", "Server=localhost;Database=Crm")])
            .Build();
        var services = new ServiceCollection();
        new OrganisationModule().AddServices(services, configuration);
        return services;
    }

    private static ServiceDescriptor Single(ServiceCollection services, Type serviceType) =>
        Assert.Single(services, d => d.ServiceType == serviceType);

    [Fact]
    public void TheNameIsOrganisation()
    {
        Assert.Equal("Organisation", new OrganisationModule().Name);
    }

    [Fact]
    public void TheApplicationAssemblyIsTheModulesApplicationAssembly()
    {
        Assert.Same(typeof(OrganisationErrors).Assembly, new OrganisationModule().ApplicationAssembly);
    }

    [Fact]
    public void TheModuleApiIsRegisteredScoped()
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(IOrganisationModuleApi));

        Assert.Equal(typeof(OrganisationModuleApi), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheSeederIsRegisteredScoped()
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(IModuleSeeder));

        Assert.Equal(typeof(OrganisationSeeder), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheSvgSanitizerIsRegisteredAsASingleton()
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(ISvgSanitizer));

        Assert.Equal(typeof(SvgSanitizer), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(OrganisationAggregate), typeof(TenantId))]
    [InlineData(typeof(Branch), typeof(BranchId))]
    [InlineData(typeof(Department), typeof(DepartmentId))]
    [InlineData(typeof(Membership), typeof(MembershipId))]
    [InlineData(typeof(BrandLogo), typeof(BrandLogoId))]
    public void EveryAggregateHasAScopedRepositoryOverTheModuleContext(Type aggregate, Type id)
    {
        Type serviceType = typeof(IRepository<,>).MakeGenericType(aggregate, id);

        ServiceDescriptor descriptor = Single(Registered(), serviceType);

        Assert.Equal(typeof(Repository<,,>).MakeGenericType(aggregate, id, typeof(OrganisationDbContext)), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheUnitOfWorkAndTheContextAreRegistered()
    {
        ServiceCollection services = Registered();

        Assert.Contains(services, d => d.ServiceType == typeof(OrganisationDbContext));
        Assert.Equal(ServiceLifetime.Scoped, Single(services, typeof(IOrganisationUnitOfWork)).Lifetime);
    }

    [Fact]
    public void TheModuleIsRecordedForTheUnitOfWorkResolverAndTheMigrator()
    {
        ServiceCollection services = Registered();

        ServiceDescriptor unitOfWork = Single(services, typeof(ModuleUnitOfWork));
        Assert.Equal(
            new ModuleUnitOfWork("Organisation", typeof(OrganisationErrors).Assembly, typeof(IOrganisationUnitOfWork)),
            unitOfWork.ImplementationInstance);
        ServiceDescriptor database = Single(services, typeof(ModuleDatabase));
        Assert.Equal(new ModuleDatabase("Organisation", "organisation", typeof(OrganisationDbContext)), database.ImplementationInstance);
    }

    [Theory]
    [InlineData("en", "A colour must be a hex value like #1A2B3C.")]
    [InlineData("ar", "يجب أن يكون اللون قيمة سداسية عشرية مثل #1A2B3C.")]
    public void TheLocalizationResourceResolvesTheColourMessageInBothLanguages(string culture, string expected)
    {
        LocalizationResource resource = Assert.IsType<LocalizationResource>(Single(Registered(), typeof(LocalizationResource)).ImplementationInstance);

        string? message = resource.Manager.GetString(OrganisationMessageKeys.Validation.ColourInvalid, CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expected, message);
    }

    [Fact]
    public void AddServicesNeedsAConnectionString()
    {
        var services = new ServiceCollection();
        IConfiguration empty = new ConfigurationBuilder().Build();

        Assert.ThrowsAny<Exception>(() => new OrganisationModule().AddServices(services, empty));
    }
}
