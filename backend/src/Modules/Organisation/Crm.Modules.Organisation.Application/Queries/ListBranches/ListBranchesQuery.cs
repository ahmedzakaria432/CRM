using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListBranches;

public sealed record ListBranchesQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<BranchDto>>>;
