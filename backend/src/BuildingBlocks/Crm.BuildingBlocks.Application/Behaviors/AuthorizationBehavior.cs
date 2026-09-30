using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Application.Behaviors;

/// <summary>Story 07: enforces the request's authorisation declaration (CLAUDE.md, Authorisation); the handler
/// never runs on a 401/403. A request with no HttpContext (seeders, background code) is a trusted in-process
/// caller and passes. A portal principal (Story 47) is refused on any request that names no permission
/// ([RequireAuthenticatedUser] means staff).</summary>
public sealed partial class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserService currentUser,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Resolved once per closed type; the startup guard already proved every request declares exactly one.
    private static readonly AuthorizationRequirement Requirement = RequestAuthorizationGuard.For(typeof(TRequest));

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        OrganisationScope scope = currentUser.Scope;

        if (Requirement.AllowsAnonymous || scope.IsUnrestricted)
        {
            return await next();
        }

        if (currentUser.UserId is null)
        {
            LogUnauthenticated(typeof(TRequest).Name);
            return CreateFailureResponse(Error.Unauthorized("Authorization.Unauthenticated", "Sign in to continue."));
        }

        if (Requirement.Permission is null && scope.IsPortalCustomer)
        {
            LogStaffOnly(typeof(TRequest).Name, currentUser.UserIdOrSystem);
            return CreateFailureResponse(Error.Forbidden("Authorization.StaffOnly", "This operation is for staff users."));
        }

        if (Requirement.Permission is { } permission
            && !CrmPermissions.ForRoles(scope.EffectiveRoles).Contains(permission, StringComparer.Ordinal))
        {
            LogDenied(permission, typeof(TRequest).Name, currentUser.UserIdOrSystem, string.Join(',', scope.EffectiveRoles));
            return CreateFailureResponse(Error.Forbidden("Authorization.PermissionDenied", $"The permission '{permission}' is required."));
        }

        return await next();
    }

    private static TResponse CreateFailureResponse(Error error)
    {
        Type responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        Type valueType = responseType.GetGenericArguments()[0];
        MethodInfo failureMethod = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(valueType);

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Unauthenticated call to {RequestName} refused")]
    private partial void LogUnauthenticated(string requestName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Portal principal {UserId} refused staff-only {RequestName}")]
    private partial void LogStaffOnly(string requestName, string userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Permission {Permission} denied for {RequestName} to user {UserId} (roles {Roles})")]
    private partial void LogDenied(string permission, string requestName, string userId, string roles);
}
