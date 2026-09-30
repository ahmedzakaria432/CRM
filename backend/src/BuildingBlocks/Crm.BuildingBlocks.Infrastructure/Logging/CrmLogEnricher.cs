using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Adds CorrelationId to every log line, and UserId/TenantId/DepartmentId/BranchId when the caller is
/// authenticated. UserId reads the claim directly (this is a singleton, ICurrentUserService is scoped);
/// TenantId/DepartmentId/BranchId are read from ICurrentUserService per event (Story 06: they are the
/// caller's effective scope, not a single claim value) because the enricher itself cannot hold scoped state.
/// </summary>
public sealed class CrmLogEnricher(ICorrelationIdAccessor correlation, IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        if (correlation.CorrelationId is { } correlationId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        ClaimsPrincipal? user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (user.FindFirst(CrmClaimTypes.UserId)?.Value is { } userId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
        }

        ICurrentUserService? currentUser = httpContext?.RequestServices.GetService<ICurrentUserService>();
        if (currentUser is null)
        {
            return;
        }

        if (currentUser.TenantId is { } tenantId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenantId));
        }

        if (currentUser.DepartmentId is { } departmentId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DepartmentId", departmentId));
        }

        if (currentUser.BranchId is { } branchId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("BranchId", branchId));
        }
    }
}
