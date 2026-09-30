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

    public static Error PortalContactNotFound(Guid contactId) =>
        Error.NotFound("Identity.PortalContactNotFound", $"Contact '{contactId}' was not found on this customer.");

    public static Error PortalAccessNotEnabled() =>
        Error.Domain("Identity.PortalAccessNotEnabled", "Enable portal access on the contact first.");

    public static Error PortalCustomerInactive() =>
        Error.Domain("Identity.PortalCustomerInactive", "The customer is inactive.");

    public static Error PortalEmailInUse() =>
        Error.Conflict("Identity.PortalEmailInUse", "Another portal account already uses this email.");

    public static Error PortalOnly() =>
        Error.Unauthorized("Authorization.PortalOnly", "Sign in to the customer portal to continue.");
}
