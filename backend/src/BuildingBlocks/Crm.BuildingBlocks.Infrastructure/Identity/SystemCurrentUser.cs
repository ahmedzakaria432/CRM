using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Infrastructure.Identity;

/// <summary>Used only by design-time DbContext factories, where no HttpContext exists at all. Never registered
/// in DI - CurrentUserService (scoped, HttpContext-backed) is the only DI-resolved ICurrentUserService.</summary>
public sealed class SystemCurrentUser : ICurrentUserService
{
    public static readonly SystemCurrentUser Instance = new();

    private SystemCurrentUser()
    {
    }

    public string? UserId => null;

    public string? SessionId => null;

    public string UserIdOrSystem => "system";

    public string? TenantId => null;

    public string? DepartmentId => null;

    public string? BranchId => null;

    public OrganisationScope Scope => OrganisationScope.Unrestricted;
}
