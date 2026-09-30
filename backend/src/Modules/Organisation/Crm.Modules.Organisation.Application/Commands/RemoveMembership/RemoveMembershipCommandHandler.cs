using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.RemoveMembership;

public sealed class RemoveMembershipCommandHandler(IRepository<Membership, MembershipId> repository, IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<RemoveMembershipCommand, Result>
{
    public async Task<Result> Handle(RemoveMembershipCommand request, CancellationToken cancellationToken)
    {
        var id = new MembershipId(request.Id);
        Membership? membership = await repository.GetByIdAsync(id, cancellationToken);

        if (membership is null)
        {
            return Result.Failure(Error.NotFound("Organisation.MembershipNotFound", $"Membership '{request.Id}' was not found."));
        }

        repository.Remove(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
