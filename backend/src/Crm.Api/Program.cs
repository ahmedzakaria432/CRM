using Crm.Api.Endpoints;
using Crm.Application;
using Crm.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi(); // Story 04 generates its typed client from this document.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(); // Story 03 replaces this with the global exception handler.

// Liveness: the process is up. Must return 200 even when SQL Server is down, so it runs no
// registered checks at all - MapHealthChecks evaluates every registered check by default, which
// would otherwise pull the SQL Server check (registered with no exclusion) into liveness too.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness: filtered to the "ready" tag. Returns 503 when SQL Server is unreachable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapEndpointGroups();

app.Run();
