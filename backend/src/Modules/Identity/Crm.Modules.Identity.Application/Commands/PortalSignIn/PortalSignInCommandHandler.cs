using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Application.Commands.PortalSignIn;

public sealed partial class PortalSignInCommandHandler(
    IRepository<PortalAccount, PortalAccountId> accounts,
    IRepository<PortalSession, PortalSessionId> sessions,
    IPasswordHasher hasher,
    IRefreshTokenGenerator generator,
    IAccessTokenIssuer issuer,
    ICustomersModuleApi customers,
    SessionSettings settings,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork,
    ILogger<PortalSignInCommandHandler> logger)
    : IRequestHandler<PortalSignInCommand, Result<SignInOutcome>>
{
    public async Task<Result<SignInOutcome>> Handle(PortalSignInCommand request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTime.UtcNow;

        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("malformed", null);
            return Failed();
        }

        PortalAccount? account = await accounts.FirstOrDefaultAsync(new PortalAccountByEmailSpecification(email), cancellationToken);

        if (account is null)
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("unknown", null);
            return Failed();
        }

        if (account.IsLockedOut(now))
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("locked", account.Id.Value);
            return Failed();
        }

        if (!hasher.Verify(account.PasswordHash, request.Password))
        {
            account.RecordFailedSignIn(now, settings.Lockout);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (account.IsLockedOut(now))
            {
                LogLockedOut(account.Id.Value, account.LockedOutUntilUtc);
            }
            else
            {
                LogRefused("wrong-password", account.Id.Value);
            }

            return Failed();
        }

        // No integration event may be published before this call: the TransactionBehavior of a nested query would
        // throw on a pending event. A contact that lost portal access (or whose customer is inactive) is refused (D15).
        Result<PortalContactMatch> contact = await customers.GetPortalContactAsync(account.ContactId, cancellationToken);

        if (contact.IsFailure)
        {
            LogRefused("portal-access-revoked", account.Id.Value);
            return Failed();
        }

        NewRefreshToken refresh = generator.Create();
        var session = PortalSession.Start(account.Id, refresh.TokenHash, now, now + settings.RefreshTokenLifetime);

        if (PortalAccessTokenSubjectFactory.Build(account, contact.Value, session.Id) is not { } subject)
        {
            LogRefused("binding-mismatch", account.Id.Value);
            return Failed();
        }

        sessions.Add(session);
        IssuedAccessToken access = issuer.IssuePortal(subject, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogSignedIn(account.Id.Value, session.Id.Value);

        return Result.Success(SignInOutcome.Success(new SessionTokens(
            access.Token, access.ExpiresAtUtc, refresh.Token, session.Tokens.Single().ExpiresAtUtc)));
    }

    private static Result<SignInOutcome> Failed() =>
        Result.Success(SignInOutcome.Failed(IdentityErrors.InvalidCredentials));

    // Never the email, the password, or the request: only the reason and the account id when one is known.
    [LoggerMessage(Level = LogLevel.Information, Message = "Portal sign-in refused ({Reason}) for account {AccountId}")]
    private partial void LogRefused(string reason, Guid? accountId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Portal sign-in refused (wrong-password) for account {AccountId}: account locked until {Until}")]
    private partial void LogLockedOut(Guid accountId, DateTimeOffset? until);

    [LoggerMessage(Level = LogLevel.Information, Message = "Portal account {AccountId} signed in (session {SessionId})")]
    private partial void LogSignedIn(Guid accountId, Guid sessionId);
}
