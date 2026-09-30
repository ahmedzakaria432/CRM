using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListBranches;

public sealed class ListBranchesQueryHandler(IRepository<Branch, BranchId> repository)
    : IRequestHandler<ListBranchesQuery, Result<IReadOnlyList<BranchDto>>>
{
    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Branch> branches = await repository.ListAsync(new BranchesSpecification(request.ActiveOnly), cancellationToken);
        return Result.Success<IReadOnlyList<BranchDto>>(branches.Select(BranchDto.FromDomain).ToList());
    }
}
