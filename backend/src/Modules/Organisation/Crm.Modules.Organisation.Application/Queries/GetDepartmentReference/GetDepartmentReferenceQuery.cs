using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetDepartmentReference;

/// <summary>In-process only (IOrganisationModuleApi); no HTTP endpoint.</summary>
[RequireAuthenticatedUser]
public sealed record GetDepartmentReferenceQuery(Guid DepartmentId) : IRequest<Result<DepartmentReference>>;
