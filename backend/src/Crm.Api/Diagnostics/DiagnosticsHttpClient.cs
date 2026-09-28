using Crm.BuildingBlocks.Infrastructure.Http;

namespace Crm.Api.Diagnostics;

/// <summary>The resilient client the Outbound diagnostics endpoint uses to call the host's own Unstable
/// endpoint - how the retry and circuit-breaker behaviour is verified by hand. Development only.</summary>
internal static class DiagnosticsHttpClient
{
    public const string Name = "Diagnostics";

    public static IServiceCollection AddDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(Name, client =>
                client.BaseAddress = new Uri(configuration["Diagnostics:SelfBaseUrl"] ?? "http://localhost:5187/"))
            .AddCrmResilience(configuration);

        return services;
    }
}
