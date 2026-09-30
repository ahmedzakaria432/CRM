using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Tickets.Application;

/// <summary>The caller as a Guid. Guid.Empty (no parsable user) becomes 422 Tickets.ActorRequired from the aggregate.</summary>
internal static class CurrentActor
{
    public static Guid Id(ICurrentUserService user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Guid.TryParse(user.UserId, CultureInfo.InvariantCulture, out Guid id) ? id : Guid.Empty;
    }
}
