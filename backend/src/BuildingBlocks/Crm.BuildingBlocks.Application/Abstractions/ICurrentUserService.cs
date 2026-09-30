namespace Crm.BuildingBlocks.Application.Abstractions;

public interface ICurrentUserService
{
    string? UserId { get; }

    /// <summary>"system" when unauthenticated - seeder, background jobs, migrations.</summary>
    string UserIdOrSystem { get; }

    /// <summary>The caller's tenant id, or null when unauthenticated.</summary>
    string? TenantId { get; }

    /// <summary>Effective department ids, sorted and comma-joined (log + cache-key segment); null when none.</summary>
    string? DepartmentId { get; }

    /// <summary>Effective branch ids, sorted and comma-joined; null when none.</summary>
    string? BranchId { get; }

    /// <summary>The resolved data scope (Story 06). Never null.</summary>
    OrganisationScope Scope { get; }
}
