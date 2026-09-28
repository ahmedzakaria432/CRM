namespace Crm.BuildingBlocks.Application.Abstractions;

public interface ICurrentUserService
{
    string? UserId { get; }

    /// <summary>"system" when unauthenticated - seeder, background jobs, migrations.</summary>
    string UserIdOrSystem { get; }

    /// <summary>Null until Story 06/07 put the scope claims (<see cref="CrmClaimTypes"/>) on the token.</summary>
    string? TenantId { get; }

    string? DepartmentId { get; }

    string? BranchId { get; }
}
