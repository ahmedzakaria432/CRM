using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Application.Commands.RefreshSession;

public sealed partial class RefreshSessionCommandHandler(
    IRepository<User, UserId> users,
    IRepository<RefreshTokenFamily, RefreshTokenFamilyId> families,
    IRefreshTokenGenerator generator,
    IAccessTokenIssuer issuer,
    IOrganisationModuleApi organisation,
    SessionSettings settings,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork,
    ILogger<RefreshSessionCommandHandler> logger)
    : IRequestHandler<RefreshSessionCommand, Result<SignInOutcome>>
{
    public async Task<Result<SignInOutcome>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTime.UtcNow;
        string hash = generator.Hash(request.RefreshToken);

        RefreshTokenFamily? family = await families.FirstOrDefaultAsync(
            new RefreshTokenFamilyByTokenHashSpecification(hash), cancellationToken);

        if (family is null)
        {
            LogUnknownToken();
            return Failed();
        }

        User? user = await users.GetByIdAsync(family.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            family.Revoke(SessionRevocationReason.UserDeactivated, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failed();
        }

        NewRefreshToken next = generator.Create();
        RefreshTokenRotation rotation = family.Rotate(hash, next.TokenHash, now, now + settings.RefreshTokenLifetime);

        switch (rotation)
        {
            case RefreshTokenRotation.ReuseDetected:
                await unitOfWork.SaveChangesAsync(cancellationToken);
                LogReuse(family.Id.Value, user.Id.Value);
                return Failed();

            case RefreshTokenRotation.Expired:
            case RefreshTokenRotation.Revoked:
                return Failed();
        }

        Result<UserMemberships> memberships = await organisation.GetMembershipsAsync(user.Id.Value, cancellationToken);

        if (memberships.IsFailure)
        {
            return Result.Failure<SignInOutcome>(memberships.Error);
        }

        IssuedAccessToken access = issuer.Issue(AccessTokenSubjectFactory.Build(user, memberships.Value, family.Id), now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SignInOutcome.Success(new SessionTokens(
            access.Token, access.ExpiresAtUtc, next.Token, now + settings.RefreshTokenLifetime)));
    }

    private static Result<SignInOutcome> Failed() =>
        Result.Success(SignInOutcome.Failed(IdentityErrors.InvalidRefreshToken));

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh refused: unknown token")]
    private partial void LogUnknownToken();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected; session {SessionId} of user {UserId} revoked")]
    private partial void LogReuse(Guid sessionId, Guid userId);
}
