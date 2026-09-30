using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.RenameBranch;

public sealed class RenameBranchCommandHandler(IRepository<Branch, BranchId> repository, IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<RenameBranchCommand, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(RenameBranchCommand request, CancellationToken cancellationToken)
    {
        var id = new BranchId(request.Id);
        Branch? branch = await repository.GetByIdAsync(id, cancellationToken);

        if (branch is null)
        {
            return Result.Failure<BranchDto>(Error.NotFound("Organisation.BranchNotFound", $"Branch '{request.Id}' was not found."));
        }

        repository.SetOriginalRowVersion(branch, request.RowVersion);
        branch.Rename(LocalizedText.Create(request.NameAr, request.NameEn));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BranchDto.FromDomain(branch));
    }
}
