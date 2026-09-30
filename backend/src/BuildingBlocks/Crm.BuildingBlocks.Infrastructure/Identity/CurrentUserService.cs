using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Scoping;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Infrastructure.Identity;

// Story 06: scope from membership claims + X-Crm-Scope. Story 07: the claims come from the staff JWT (Identity module).
// Story 47: a Portal-scheme identity resolves to a customer grant (tenant + PortalCustomerGrant, no departments).
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    // Cached per principal INSTANCE, not per request: an authentication handler that resolves this service
    // before HttpContext.User is set would otherwise pin OrganisationScope.None for the rest of the request.
    private ClaimsPrincipal? _cachedUser;
    private OrganisationScope? _cachedScope;

    public string? UserId => Claim(CrmClaimTypes.UserId);

    public string? SessionId => Claim(CrmClaimTypes.SessionId);

    public string UserIdOrSystem => UserId ?? "system";

    public string? TenantId => Scope.Tenant == Crm.SharedKernel.Scoping.TenantId.None ? null : Scope.Tenant.Value.ToString();

    public string? DepartmentId => Join(Scope.Departments.Select(d => d.Value));

    public string? BranchId => Join(Scope.Branches.Select(b => b.Value));

    public OrganisationScope Scope
    {
        get
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return OrganisationScope.Unrestricted;
            }

            if (_cachedScope is not null && ReferenceEquals(_cachedUser, httpContext.User))
            {
                return _cachedScope;
            }

            OrganisationScope scope = Resolve(httpContext);
            _cachedUser = httpContext.User;
            _cachedScope = scope;
            return scope;
        }
    }

    private static OrganisationScope Resolve(HttpContext httpContext)
    {
        ClaimsPrincipal user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return OrganisationScope.None;
        }

        string? tenantClaim = user.FindFirst(CrmClaimTypes.TenantId)?.Value;
        if (tenantClaim is null || !Guid.TryParse(tenantClaim, out Guid tenantGuid))
        {
            return OrganisationScope.None;
        }

        var tenant = new TenantId(tenantGuid);

        bool portal = user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == CrmAuthenticationSchemes.Portal);
        if (portal)
        {
            // Never mixed with staff claims; X-Crm-Scope is ignored (a portal principal has no departments to narrow).
            return user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType != CrmAuthenticationSchemes.Portal)
                ? OrganisationScope.None
                : ResolvePortal(user, tenant);
        }

        List<ScopeMembership> memberships = [];
        foreach (Claim claim in user.FindAll(CrmClaimTypes.Membership))
        {
            string[] parts = claim.Value.Split('|');
            if (parts.Length != 3
                || !Guid.TryParse(parts[0], out Guid departmentGuid)
                || !Guid.TryParse(parts[1], out Guid branchGuid))
            {
                continue; // a malformed claim is skipped, never thrown
            }

            memberships.Add(new ScopeMembership(new DepartmentId(departmentGuid), new BranchId(branchGuid), parts[2]));
        }

        string? header = httpContext.Request.Headers[OrganisationScope.HeaderName].FirstOrDefault();
        DepartmentId? activeDepartment = null;
        bool activeScopeRejected = false;

        if (!string.IsNullOrWhiteSpace(header) && !string.Equals(header, OrganisationScope.AllDepartments, StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(header, out Guid requestedGuid) && memberships.Any(m => m.DepartmentId.Value == requestedGuid))
            {
                activeDepartment = new DepartmentId(requestedGuid);
            }
            else
            {
                activeScopeRejected = true;
            }
        }

        return new OrganisationScope(false, tenant, memberships, activeDepartment, activeScopeRejected);
    }

    private static OrganisationScope ResolvePortal(ClaimsPrincipal user, TenantId tenant)
    {
        if (!Guid.TryParse(user.FindFirst(CrmClaimTypes.PortalCustomerId)?.Value, out Guid customerId)
            || !Guid.TryParse(user.FindFirst(CrmClaimTypes.PortalContactId)?.Value, out Guid contactId)
            || !bool.TryParse(user.FindFirst(CrmClaimTypes.PortalAccountAdmin)?.Value, out bool isAdmin)
            || !Guid.TryParse(user.FindFirst(CrmClaimTypes.PortalBranchId)?.Value, out Guid branchId)
            || !Guid.TryParse(user.FindFirst(CrmClaimTypes.PortalDepartmentId)?.Value, out Guid departmentId)
            || tenant == Crm.SharedKernel.Scoping.TenantId.None
            || customerId == Guid.Empty
            || contactId == Guid.Empty)
        {
            return OrganisationScope.None; // a malformed claim never throws and never widens
        }

        return OrganisationScope.ForPortalCustomer(
            tenant, new PortalCustomerGrant(customerId, contactId, isAdmin, new BranchId(branchId), new DepartmentId(departmentId)));
    }

    private static string? Join(IEnumerable<Guid> values)
    {
        string joined = string.Join(',', values.Select(v => v.ToString()));
        return joined.Length == 0 ? null : joined;
    }

    private string? Claim(string type)
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true ? user.FindFirst(type)?.Value : null;
    }
}
