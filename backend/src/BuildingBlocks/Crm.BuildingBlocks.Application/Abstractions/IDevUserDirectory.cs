namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>DEVELOPMENT ONLY (Story 06). Lets the X-Dev-User handler in BuildingBlocks build a principal from
/// the Organisation module's memberships without referencing the module. Deleted by Story 07.</summary>
public interface IDevUserDirectory
{
    Task<DevUser?> FindAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record DevUser(Guid UserId, Guid TenantId, IReadOnlyList<DevUserMembership> Memberships);

public sealed record DevUserMembership(Guid DepartmentId, Guid BranchId, string Role);
