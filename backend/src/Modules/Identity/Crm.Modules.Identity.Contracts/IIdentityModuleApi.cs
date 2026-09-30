using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Contracts;

/// <summary>What another module may ask the Identity module, synchronously (Story 15: assignee names and the
/// "already claimed by" message). Only users of the caller's tenant are returned; unknown ids are simply absent.</summary>
public interface IIdentityModuleApi
{
    Task<Result<IReadOnlyList<StaffUserSummary>>> GetStaffUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

public sealed record StaffUserSummary(Guid Id, string DisplayName, bool IsActive);
