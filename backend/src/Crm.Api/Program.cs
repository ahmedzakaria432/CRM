using Crm.Api.Modules;
using Crm.BuildingBlocks.Application;
using Crm.BuildingBlocks.Infrastructure;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Presentation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

// Shared building blocks first, then every module. MediatR is registered once, over every module's
// Application assembly - a module never registers it itself.
builder.Services.AddBuildingBlocksApplication([.. ModuleCatalog.All.Select(m => m.Module.ApplicationAssembly)]);
builder.Services.AddBuildingBlocksInfrastructure(builder.Configuration, builder.Environment);

foreach (ModuleEntry entry in ModuleCatalog.All)
{
    entry.Module.AddServices(builder.Services, builder.Configuration);
}

IMvcBuilder mvc = builder.Services.AddControllers();

foreach (ModuleEntry entry in ModuleCatalog.All)
{
    mvc.AddApplicationPart(entry.PresentationAssembly);
}

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

    // Routes each action into the document for its Area, whichever module declares it. An action on
    // a controller that forgot [Area(...)] carries no "area" route value and appears in no document.
    options.DocInclusionPredicate((docName, apiDescription) =>
        apiDescription.ActionDescriptor.RouteValues.TryGetValue("area", out string? area)
        && string.Equals(area, docName, StringComparison.OrdinalIgnoreCase));

    // Controllers live in the module Presentation assemblies, so their XML doc files are read too.
    foreach (string xmlDoc in Directory.EnumerateFiles(AppContext.BaseDirectory, "Crm.*.xml"))
    {
        options.IncludeXmlComments(xmlDoc);
    }
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

    // Migrate every module (each into its own schema) and seed only in Development - deployment
    // applies migrations as an explicit step, not as a side effect of the process starting.
    await app.Services.MigrateAndSeedModulesAsync();
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
