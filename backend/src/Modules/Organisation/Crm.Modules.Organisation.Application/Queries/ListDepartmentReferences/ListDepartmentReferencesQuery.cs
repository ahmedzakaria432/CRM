using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListDepartmentReferences;

/// <summary>In-process only (IOrganisationModuleApi); no HTTP endpoint.</summary>
[RequireAuthenticatedUser]
public sealed record ListDepartmentReferencesQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<DepartmentReference>>>;
