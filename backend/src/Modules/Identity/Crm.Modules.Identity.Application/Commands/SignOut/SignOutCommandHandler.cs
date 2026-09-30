using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.SignOut;

public sealed class SignOutCommandHandler(
    IRepository<RefreshTokenFamily, RefreshTokenFamilyId> families,
    IRefreshTokenGenerator generator,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<SignOutCommand, Result>
{
    public async Task<Result> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Success();
        }

        RefreshTokenFamily? family = await families.FirstOrDefaultAsync(
            new RefreshTokenFamilyByTokenHashSpecification(generator.Hash(request.RefreshToken)), cancellationToken);

        if (family is not null)
        {
            family.Revoke(SessionRevocationReason.SignedOut, dateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
