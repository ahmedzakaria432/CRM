using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>THE HAND-WRITTEN TENANT FILTER: User is deliberately not ITenantScoped (sign-in precedes the scope),
/// so the tenant criterion is written here and every admin user query must go through this specification.
/// Search matches email or display name; the list is ordered by display name. Paging applies only when both
/// page arguments are given (a count passes neither).</summary>
public sealed class UsersSpecification : Specification<User>
{
    public UsersSpecification(TenantId tenant, string? search, int? page, int? pageSize)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        Where(u => u.TenantId == tenant
            && (term == null || u.Email.Value.Contains(term) || u.DisplayName.Value.Contains(term)));

        ApplyOrderBy(u => u.DisplayName.Value);

        if (page is { } p && pageSize is { } size)
        {
            ApplyPaging((p - 1) * size, size);
        }

        AsReadOnly();
    }
}
