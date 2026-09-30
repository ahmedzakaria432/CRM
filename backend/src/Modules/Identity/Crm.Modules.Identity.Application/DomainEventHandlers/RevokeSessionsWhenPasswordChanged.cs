using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using MediatR;

namespace Crm.Modules.Identity.Application.DomainEventHandlers;

/// <summary>A self-service change keeps the caller's own session; an administrator reset keeps none.</summary>
public sealed class RevokeSessionsWhenPasswordChanged(
    IRepository<RefreshTokenFamily, RefreshTokenFamilyId> families,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : INotificationHandler<DomainEventNotification<UserPasswordChanged>>
{
    public async Task Handle(DomainEventNotification<UserPasswordChanged> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        UserPasswordChanged changed = notification.DomainEvent;

        IReadOnlyList<RefreshTokenFamily> active = await families.ListAsync(
            new ActiveRefreshTokenFamiliesOfUserSpecification(changed.UserId), cancellationToken);

        SessionRevocationReason reason = changed.KeepSessionId is null
            ? SessionRevocationReason.PasswordReset
            : SessionRevocationReason.PasswordChanged;

        DateTimeOffset now = dateTime.UtcNow;

        foreach (RefreshTokenFamily family in active.Where(f => f.Id.Value != changed.KeepSessionId))
        {
            family.Revoke(reason, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
