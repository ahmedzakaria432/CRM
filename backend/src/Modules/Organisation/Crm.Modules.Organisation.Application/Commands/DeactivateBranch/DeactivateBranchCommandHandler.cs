using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.DeactivateBranch;

public sealed class DeactivateBranchCommandHandler(IRepository<Branch, BranchId> repository, IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateBranchCommand, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var id = new BranchId(request.Id);
        Branch? branch = await repository.GetByIdAsync(id, cancellationToken);

        if (branch is null)
        {
            return Result.Failure<BranchDto>(Error.NotFound("Organisation.BranchNotFound", $"Branch '{request.Id}' was not found."));
        }

        repository.SetOriginalRowVersion(branch, request.RowVersion);
        branch.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BranchDto.FromDomain(branch));
    }
}
