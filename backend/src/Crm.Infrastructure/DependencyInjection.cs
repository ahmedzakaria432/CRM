using Crm.Application.Common.Abstractions;
using Crm.Domain.Common;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Crm.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: configuration.GetConnectionString("Crm")
                    ?? throw new InvalidOperationException("Connection string 'Crm' is not configured."),
                name: "sql-server",
                tags: ["ready"]);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<CrmDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Crm"));
            options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());

            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CrmDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
