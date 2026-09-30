using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.CreateBranch;

public sealed class CreateBranchCommandHandler(
    IRepository<Branch, BranchId> repository,
    IOrganisationUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateBranchCommand, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        TenantId tenant = currentUser.Scope.Tenant;
        if (tenant == TenantId.None)
        {
            return Result.Failure<BranchDto>(Error.Forbidden("Organisation.TenantRequired", "No tenant is associated with the caller."));
        }

        OrganisationCode code = OrganisationCode.Create(request.Code);

        int taken = await repository.CountAsync(new BranchByCodeSpecification(code), cancellationToken);
        if (taken > 0)
        {
            return Result.Failure<BranchDto>(Error.Conflict("Organisation.BranchCodeTaken", $"Branch code '{code.Value}' is already in use."));
        }

        Branch branch = Branch.Create(tenant, code, LocalizedText.Create(request.NameAr, request.NameEn));
        repository.Add(branch);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BranchDto.FromDomain(branch));
    }
}
