using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;

public sealed class ChangeMembershipRoleCommandHandler(IRepository<Membership, MembershipId> repository, IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<ChangeMembershipRoleCommand, Result<MembershipDto>>
{
    public async Task<Result<MembershipDto>> Handle(ChangeMembershipRoleCommand request, CancellationToken cancellationToken)
    {
        var id = new MembershipId(request.Id);
        Membership? membership = await repository.GetByIdAsync(id, cancellationToken);

        if (membership is null)
        {
            return Result.Failure<MembershipDto>(Error.NotFound("Organisation.MembershipNotFound", $"Membership '{request.Id}' was not found."));
        }

        repository.SetOriginalRowVersion(membership, request.RowVersion);
        membership.ChangeRole(Enum.Parse<SystemRole>(request.Role, false));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(MembershipDto.FromDomain(membership));
    }
}
