using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Modules;
using Crm.SharedKernel;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Application.Behaviors;

public sealed partial class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWorkResolver unitOfWorkResolver,
    IDomainEventDispatcher dispatcher,
    IPendingIntegrationEvents pendingIntegrationEvents,
    ICorrelationIdAccessor correlation,
    ICacheService cache,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Queries do not open a transaction - only commands do (CLAUDE.md, "Backend", CQRS:
        // "Commands go through aggregates; queries bypass them"). Without this guard every read
        // would open and commit a pointless SQL Server transaction on the hot path.
        if (request is not ICommandMarker)
        {
            TResponse queryResponse = await next();
            ThrowIfPublishedOutsideCommand("a query");
            return queryResponse;
        }

        // Resolved per request, not injected: every module has its own DbContext, and a command
        // must run in the transaction of the module that owns it (one transaction, one module).
        IUnitOfWork unitOfWork = unitOfWorkResolver.ForRequest(typeof(TRequest));

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            TResponse response = await next();

            // A failed Result must not commit. Business failures are results, not exceptions
            // (CLAUDE.md, "Backend") - this is the only place that can see that before commit.
            if (response is Result { IsFailure: true })
            {
                pendingIntegrationEvents.DequeueAll(); // a failed command publishes nothing
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return response;
            }

            // Integration events become outbox rows INSIDE this transaction - they commit or roll back
            // with the change that raised them (Story 03). The dispatcher delivers them after commit.
            IReadOnlyList<IIntegrationEvent> integrationEvents = pendingIntegrationEvents.DequeueAll();
            if (integrationEvents.Count > 0)
            {
                unitOfWork.AddToOutbox(integrationEvents, correlation.CorrelationId);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Harvest and clear BEFORE commit, dispatch AFTER it. Harvesting after commit risks
            // losing events to a handler that already cleared the change tracker; dispatching
            // before commit would publish events for work that could still roll back - verified by
            // probe: with an explicit transaction open, the point where SaveChanges completes is
            // NOT the point where the transaction is durable.
            IReadOnlyCollection<IDomainEvent> domainEvents = unitOfWork.GetAndClearDomainEvents();

            await unitOfWork.CommitTransactionAsync(cancellationToken); // past this line the write is durable

            if (request is ICacheInvalidatingCommand invalidating)
            {
                // After commit, never before: invalidating inside the transaction would let a concurrent
                // read re-cache the old committed row before this one commits.
                await cache.InvalidateTagsAsync(invalidating.CacheTagsToInvalidate, cancellationToken);
            }

            await dispatcher.DispatchAsync(domainEvents, cancellationToken);

            ThrowIfPublishedOutsideCommand("a domain-event handler, after commit");

            return response;
        }
        catch (ConcurrencyConflictException ex)
        {
            pendingIntegrationEvents.DequeueAll();
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            LogConcurrencyConflict(typeof(TRequest).Name, ex);

            return BuildConflictResponse();
        }
        catch (OrganisationScopeViolationException)
        {
            // Explicit rollback, then let OrganisationScopeBehavior (outer) turn it into 403.
            pendingIntegrationEvents.DequeueAll();
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    // Loud on purpose: an event published where no outbox write follows would be silently lost.
    private void ThrowIfPublishedOutsideCommand(string where)
    {
        if (pendingIntegrationEvents.HasPending)
        {
            pendingIntegrationEvents.DequeueAll();
            throw new InvalidOperationException(
                $"An integration event was published from {where} while handling '{typeof(TRequest).Name}'. " +
                "Publish integration events from the command handler (CLAUDE.md, integration events).");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrency conflict handling {RequestName}")]
    private partial void LogConcurrencyConflict(string requestName, Exception exception);

    /// <summary>Builds a failed TResponse by reflection over Result.Failure / Result.Failure&lt;T&gt;
    /// - the same technique ValidationBehavior already uses for its TResponse guard.</summary>
    private static TResponse BuildConflictResponse()
    {
        Error error = Error.Conflict(
            "Persistence.ConcurrencyConflict",
            "The record was modified by someone else. Reload and try again.");

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
}
