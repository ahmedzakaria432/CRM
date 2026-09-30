using System.Reflection;
using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Modules.Tickets.Infrastructure;

/// <summary>The Tickets module (Story 15): tickets, their lifecycle and assignment, and categories. It exposes no
/// Contracts API yet - other modules learn about tickets through its integration events.</summary>
public sealed class TicketsModule : IModule
{
    public string Name => "Tickets";

    public Assembly ApplicationAssembly => AssemblyReference.Assembly;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TicketsDbContext, ITicketsUnitOfWork>(
            configuration, Name, TicketsDbContext.SchemaName, ApplicationAssembly);

        services.AddRepository<Ticket, TicketId, TicketsDbContext>();
        services.AddRepository<Category, CategoryId, TicketsDbContext>();
        services.AddScoped<ITicketNumberGenerator, SqlTicketNumberGenerator>();
        services.AddScoped<ITicketReadModel, TicketReadModel>();
        services.AddScoped<TicketDtoAssembler>();
        services.AddScoped<IModuleSeeder, TicketsSeeder>();

        // Validation and refusal messages that are server text follow Accept-Language.
        services.AddLocalizationResource(new ResourceManager(
            "Crm.Modules.Tickets.Application.Localization.Resources.TicketsMessages", ApplicationAssembly));
    }
}
