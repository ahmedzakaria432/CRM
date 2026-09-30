using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using MediatR;

namespace Crm.Modules.Identity.Application.DomainEventHandlers;

public sealed class RevokeSessionsWhenUserDeactivated(
    IRepository<RefreshTokenFamily, RefreshTokenFamilyId> families,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : INotificationHandler<DomainEventNotification<UserDeactivated>>
{
    public async Task Handle(DomainEventNotification<UserDeactivated> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        IReadOnlyList<RefreshTokenFamily> active = await families.ListAsync(
            new ActiveRefreshTokenFamiliesOfUserSpecification(notification.DomainEvent.UserId), cancellationToken);

        DateTimeOffset now = dateTime.UtcNow;

        foreach (RefreshTokenFamily family in active)
        {
            family.Revoke(SessionRevocationReason.UserDeactivated, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
