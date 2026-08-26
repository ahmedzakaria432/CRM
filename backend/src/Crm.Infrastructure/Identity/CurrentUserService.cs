using Crm.Application.Common.Abstractions;

namespace Crm.Infrastructure.Identity;

// Story 07 replaces this with the JWT principal.
public sealed class CurrentUserService : ICurrentUserService
{
    public string? UserId => null;

    public string UserIdOrSystem => UserId ?? "system";
}
