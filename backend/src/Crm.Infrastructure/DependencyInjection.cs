using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: configuration.GetConnectionString("Crm")
                    ?? throw new InvalidOperationException("Connection string 'Crm' is not configured."),
                name: "sql-server",
                tags: ["ready"]);

        return services;
    }
}
