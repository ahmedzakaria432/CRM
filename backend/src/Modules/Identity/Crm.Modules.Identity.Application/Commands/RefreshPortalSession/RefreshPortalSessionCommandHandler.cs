using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Application.Commands.RefreshPortalSession;

public sealed partial class RefreshPortalSessionCommandHandler(
    IRepository<PortalAccount, PortalAccountId> accounts,
    IRepository<PortalSession, PortalSessionId> sessions,
    IRefreshTokenGenerator generator,
    IAccessTokenIssuer issuer,
    ICustomersModuleApi customers,
    SessionSettings settings,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork,
    ILogger<RefreshPortalSessionCommandHandler> logger)
    : IRequestHandler<RefreshPortalSessionCommand, Result<SignInOutcome>>
{
    public async Task<Result<SignInOutcome>> Handle(RefreshPortalSessionCommand request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTime.UtcNow;
        string hash = generator.Hash(request.RefreshToken);

        PortalSession? session = await sessions.FirstOrDefaultAsync(
            new PortalSessionByTokenHashSpecification(hash), cancellationToken);

        if (session is null)
        {
            LogUnknownToken();
            return Failed();
        }

        PortalAccount? account = await accounts.GetByIdAsync(session.PortalAccountId, cancellationToken);

        if (account is null)
        {
            session.Revoke(SessionRevocationReason.PortalAccessRevoked, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failed();
        }

        NewRefreshToken next = generator.Create();
        RefreshTokenRotation rotation = session.Rotate(hash, next.TokenHash, now, now + settings.RefreshTokenLifetime);

        switch (rotation)
        {
            case RefreshTokenRotation.ReuseDetected:
                await unitOfWork.SaveChangesAsync(cancellationToken);
                LogReuse(session.Id.Value, account.Id.Value);
                return Failed();

            case RefreshTokenRotation.Expired:
            case RefreshTokenRotation.Revoked:
                return Failed();
        }

        // D15: portal access revoked, a deactivated customer or a removed contact end the session here.
        Result<PortalContactMatch> contact = await customers.GetPortalContactAsync(account.ContactId, cancellationToken);

        if (contact.IsFailure || PortalAccessTokenSubjectFactory.Build(account, contact.Value, session.Id) is not { } subject)
        {
            session.Revoke(SessionRevocationReason.PortalAccessRevoked, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogAccessRevoked(session.Id.Value, account.Id.Value);
            return Failed();
        }

        IssuedAccessToken access = issuer.IssuePortal(subject, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SignInOutcome.Success(new SessionTokens(
            access.Token, access.ExpiresAtUtc, next.Token, now + settings.RefreshTokenLifetime)));
    }

    private static Result<SignInOutcome> Failed() =>
        Result.Success(SignInOutcome.Failed(IdentityErrors.InvalidRefreshToken));

    [LoggerMessage(Level = LogLevel.Information, Message = "Portal refresh refused: unknown token")]
    private partial void LogUnknownToken();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Portal refresh token reuse detected; session {SessionId} of account {AccountId} revoked")]
    private partial void LogReuse(Guid sessionId, Guid accountId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Portal session {SessionId} of account {AccountId} revoked: portal access ended")]
    private partial void LogAccessRevoked(Guid sessionId, Guid accountId);
}
