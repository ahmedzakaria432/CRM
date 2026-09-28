using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Infrastructure.Identity;

// Story 07 adds the JWT handler; this reads its claims (CrmClaimTypes).
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId => Claim(CrmClaimTypes.UserId);

    public string UserIdOrSystem => UserId ?? "system";

    public string? TenantId => Claim(CrmClaimTypes.TenantId);

    public string? DepartmentId => Claim(CrmClaimTypes.DepartmentId);

    public string? BranchId => Claim(CrmClaimTypes.BranchId);

    private string? Claim(string type)
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true ? user.FindFirst(type)?.Value : null;
    }
}
