using Crm.Modules.Organisation.Application.Queries.GetMembershipsOfUser;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application;

/// <summary>Goes through the module's own query, so a cross-module read passes the same validation,
/// authorisation, and scoping pipeline as an HTTP read - there is no back door into the module.</summary>
public sealed class OrganisationModuleApi(ISender sender) : IOrganisationModuleApi
{
    public Task<Result<UserMemberships>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        sender.Send(new GetMembershipsOfUserQuery(userId), cancellationToken);
}
