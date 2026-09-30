using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.PortalSignOut;

public sealed class PortalSignOutCommandHandler(
    IRepository<PortalSession, PortalSessionId> sessions,
    IRefreshTokenGenerator generator,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<PortalSignOutCommand, Result>
{
    public async Task<Result> Handle(PortalSignOutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Success();
        }

        PortalSession? session = await sessions.FirstOrDefaultAsync(
            new PortalSessionByTokenHashSpecification(generator.Hash(request.RefreshToken)), cancellationToken);

        if (session is not null)
        {
            session.Revoke(SessionRevocationReason.SignedOut, dateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
