using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Application.Commands.SignIn;

public sealed partial class SignInCommandHandler(
    IRepository<User, UserId> users,
    IRepository<RefreshTokenFamily, RefreshTokenFamilyId> families,
    IPasswordHasher hasher,
    IRefreshTokenGenerator generator,
    IAccessTokenIssuer issuer,
    IOrganisationModuleApi organisation,
    SessionSettings settings,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork,
    ILogger<SignInCommandHandler> logger)
    : IRequestHandler<SignInCommand, Result<SignInOutcome>>
{
    public async Task<Result<SignInOutcome>> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = dateTime.UtcNow;

        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("malformed", null);
            return Failed();
        }

        User? user = await users.FirstOrDefaultAsync(new UserByEmailSpecification(email), cancellationToken);

        if (user is null)
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("unknown", null);
            return Failed();
        }

        if (!user.IsActive)
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("inactive", user.Id.Value);
            return Failed();
        }

        if (user.IsLockedOut(now))
        {
            hasher.VerifyAgainstDummy(request.Password);
            LogRefused("locked", user.Id.Value);
            return Failed();
        }

        if (!hasher.Verify(user.PasswordHash, request.Password))
        {
            user.RecordFailedSignIn(now, settings.Lockout);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (user.IsLockedOut(now))
            {
                LogLockedOut(user.Id.Value, user.LockedOutUntilUtc);
            }
            else
            {
                LogRefused("wrong-password", user.Id.Value);
            }

            return Failed();
        }

        // No integration event may be published before this call: a nested query's TransactionBehavior would
        // throw on a pending event.
        Result<UserMemberships> memberships = await organisation.GetMembershipsAsync(user.Id.Value, cancellationToken);

        if (memberships.IsFailure)
        {
            return Result.Failure<SignInOutcome>(memberships.Error);
        }

        NewRefreshToken refresh = generator.Create();
        var family = RefreshTokenFamily.Start(user.Id, refresh.TokenHash, now, now + settings.RefreshTokenLifetime);
        families.Add(family);

        IssuedAccessToken access = issuer.Issue(AccessTokenSubjectFactory.Build(user, memberships.Value, family.Id), now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogSignedIn(user.Id.Value, family.Id.Value);

        return Result.Success(SignInOutcome.Success(new SessionTokens(
            access.Token, access.ExpiresAtUtc, refresh.Token, family.Tokens.Single().ExpiresAtUtc)));
    }

    private static Result<SignInOutcome> Failed() =>
        Result.Success(SignInOutcome.Failed(IdentityErrors.InvalidCredentials));

    // Never the email, the password, or the request: only the reason and the user id when one is known.
    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in refused ({Reason}) for user {UserId}")]
    private partial void LogRefused(string reason, Guid? userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in refused (wrong-password) for user {UserId}: account locked until {Until}")]
    private partial void LogLockedOut(Guid userId, DateTimeOffset? until);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed in (session {SessionId})")]
    private partial void LogSignedIn(Guid userId, Guid sessionId);
}
