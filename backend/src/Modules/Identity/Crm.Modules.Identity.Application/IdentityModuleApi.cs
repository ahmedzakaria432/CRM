using Crm.Modules.Identity.Application.Queries.GetStaffUsers;
using Crm.Modules.Identity.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application;

/// <summary>Goes through the module's own query, so a cross-module read passes the same validation,
/// authorisation, and scoping pipeline as an HTTP read - there is no back door into the module.</summary>
public sealed class IdentityModuleApi(ISender sender) : IIdentityModuleApi
{
    public Task<Result<IReadOnlyList<StaffUserSummary>>> GetStaffUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        sender.Send(new GetStaffUsersQuery([.. userIds]), cancellationToken);
}
