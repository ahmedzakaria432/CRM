using System.Reflection;
using Crm.Api.Areas;
using Crm.Application;
using Crm.Infrastructure;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    foreach (string area in CrmAreas.All)
    {
        options.SwaggerDoc(area.ToLowerInvariant(), new OpenApiInfo
        {
            Title = $"CRM {area} API",
            Version = "v1",
        });
    }

    // Routes each action into the document for its Area. An action on a controller that forgot
    // [Area(...)] carries no "area" route value and appears in no document at all.
    options.DocInclusionPredicate((docName, apiDescription) =>
        apiDescription.ActionDescriptor.RouteValues.TryGetValue("area", out string? area)
        && string.Equals(area, docName, StringComparison.OrdinalIgnoreCase));

    options.IncludeXmlComments(Path.Combine(
        AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (string area in CrmAreas.All)
        {
            options.SwaggerEndpoint($"/swagger/{area.ToLowerInvariant()}/swagger.json", $"CRM {area} API");
        }
    });

    // Migrate and seed only in Development - deployment applies migrations as an explicit step,
    // not as a side effect of the process starting.
    await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        await dbContext.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
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

app.MapControllers();

app.Run();
