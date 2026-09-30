using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListDepartments;

public sealed record ListDepartmentsQuery(Guid? BranchId, bool ActiveOnly) : IRequest<Result<IReadOnlyList<DepartmentDto>>>;
