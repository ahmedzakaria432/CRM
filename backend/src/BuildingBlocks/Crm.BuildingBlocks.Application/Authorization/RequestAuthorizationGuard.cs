using System.Reflection;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.BuildingBlocks.Application.Authorization;

/// <summary>What a request declared: anonymous, any signed-in user, or a named permission.</summary>
public sealed record AuthorizationRequirement(bool AllowsAnonymous, string? Permission);

/// <summary>Story 07: the app refuses to start when any MediatR request in any module lacks exactly one
/// authorisation declaration, names an unknown permission, or cannot answer 401/403 through a Result.</summary>
public static class RequestAuthorizationGuard
{
    public static void EnsureEveryRequestDeclares(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        List<string> problems = [];

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition
                    || !typeof(IBaseRequest).IsAssignableFrom(type))
                {
                    continue;
                }

                CollectProblems(type, problems);
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Every MediatR request must declare its authorisation (CLAUDE.md, Authorisation):"
                + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }
    }

    public static AuthorizationRequirement For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        if (requestType.GetCustomAttribute<AllowAnonymousRequestAttribute>(inherit: false) is not null)
        {
            return new AuthorizationRequirement(true, null);
        }

        RequirePermissionAttribute? permission = requestType.GetCustomAttribute<RequirePermissionAttribute>(inherit: false);
        return permission is not null
            ? new AuthorizationRequirement(false, permission.Permission)
            : new AuthorizationRequirement(false, null);
    }

    private static void CollectProblems(Type type, List<string> problems)
    {
        string name = type.FullName ?? type.Name;

        RequirePermissionAttribute? permission = type.GetCustomAttribute<RequirePermissionAttribute>(inherit: false);
        int declarations =
            (permission is null ? 0 : 1)
            + (type.GetCustomAttribute<RequireAuthenticatedUserAttribute>(inherit: false) is null ? 0 : 1)
            + (type.GetCustomAttribute<AllowAnonymousRequestAttribute>(inherit: false) is null ? 0 : 1);

        if (declarations != 1)
        {
            problems.Add($"{name}: declare exactly one of [RequirePermission], [RequireAuthenticatedUser], [AllowAnonymousRequest]");
        }

        if (permission is not null && !CrmPermissions.IsKnown(permission.Permission))
        {
            problems.Add($"{name}: unknown permission '{permission.Permission}'");
        }

        Type? responseType = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments()[0];

        bool isResult = responseType is not null
            && (responseType == typeof(Result)
                || (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>)));

        if (!isResult)
        {
            problems.Add($"{name}: must return Result or Result<T>");
        }
    }
}
