using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Adds CorrelationId to every log line, and UserId/TenantId/DepartmentId/BranchId when the caller is
/// authenticated. Reads claims directly rather than ICurrentUserService: this is a singleton, that is scoped.
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

        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        AddClaim(logEvent, propertyFactory, user, "UserId", CrmClaimTypes.UserId);
        AddClaim(logEvent, propertyFactory, user, "TenantId", CrmClaimTypes.TenantId);
        AddClaim(logEvent, propertyFactory, user, "DepartmentId", CrmClaimTypes.DepartmentId);
        AddClaim(logEvent, propertyFactory, user, "BranchId", CrmClaimTypes.BranchId);
    }

    private static void AddClaim(LogEvent logEvent, ILogEventPropertyFactory factory, ClaimsPrincipal user, string property, string claimType)
    {
        if (user.FindFirst(claimType)?.Value is { } value)
        {
            logEvent.AddPropertyIfAbsent(factory.CreateProperty(property, value));
        }
    }
}
