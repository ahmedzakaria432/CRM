using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Application;

internal static class IdentityErrors
{
    public const string InvalidCredentials = "Identity.InvalidCredentials";
    public const string InvalidRefreshToken = "Identity.InvalidRefreshToken";

    public static Error UserNotFound(Guid id) =>
        Error.NotFound("Identity.UserNotFound", $"User '{id}' was not found.");

    public static Error OutOfScope() =>
        Error.Forbidden("Organisation.OutOfScope", "The user belongs to another organisation.");

    public static Error Unauthenticated() =>
        Error.Unauthorized("Authorization.Unauthenticated", "Sign in to continue.");
}
