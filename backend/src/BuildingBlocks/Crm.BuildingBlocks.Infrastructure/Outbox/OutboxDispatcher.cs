using System.Diagnostics;
using System.Text.Json;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Diagnostics;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Bound from the "Outbox" configuration section.</summary>
public sealed record OutboxSettings(TimeSpan PollingInterval, int BatchSize, int MaxAttempts)
{
    public static readonly OutboxSettings Default = new(TimeSpan.FromSeconds(5), 20, 5);
}

/// <summary>
/// Polls every module's outbox and delivers each message in-process to every subscribing
/// <see cref="IIntegrationEventConsumer{TEvent}"/>. Each handler runs in its own scope and in its OWN module's
/// transaction, and records an idempotency row in that transaction - so delivery is at least once, and
/// application is once per handler. A failing handler is retried with backoff; the handlers that already
/// succeeded are skipped on the retry.
/// </summary>
public sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IntegrationEventConsumerRegistry registry,
    OutboxSettings settings,
    IDateTime dateTime,
    ICorrelationIdAccessor correlation,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAllModulesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A BackgroundService that throws stops the host. SQL Server being down for a cycle must not.
                LogCycleFailed(ex);
            }

            try
            {
                await Task.Delay(settings.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task DispatchAllModulesAsync(CancellationToken cancellationToken)
    {
        List<ModuleDatabase> databases;
        await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
        {
            databases = [.. scope.ServiceProvider.GetServices<ModuleDatabase>()];
        }

        foreach (ModuleDatabase database in databases)
        {
            await DispatchModuleAsync(database, cancellationToken);
        }
    }

    private async Task DispatchModuleAsync(ModuleDatabase database, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var publisher = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(database.DbContextType);

        IReadOnlyList<OutboxMessage> batch = await publisher.GetDueOutboxMessagesAsync(
            dateTime.UtcNow, settings.MaxAttempts, settings.BatchSize, cancellationToken);

        if (batch.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage message in batch)
        {
            using IDisposable correlationScope = correlation.BeginScope(message.CorrelationId ?? message.Id.ToString("N"));
            using IDisposable? logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["OutboxMessageId"] = message.Id,
                ["IntegrationEvent"] = message.Type,
                ["Module"] = database.ModuleName,
            });

            await DispatchMessageAsync(message, cancellationToken);
        }

        await publisher.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!registry.TryGet(message.Type, out IntegrationEventSubscription? subscription))
        {
            // Nobody subscribes - nothing to deliver. (Also what happens to rows of a renamed event type, which
            // is why event records are append-only.)
            message.MarkProcessed(dateTime.UtcNow);
            LogNoSubscriber(message.Type);
            return;
        }

        IIntegrationEvent integrationEvent;
        try
        {
            integrationEvent = JsonSerializer.Deserialize(message.Content, subscription.EventType, OutboxJson.Options) as IIntegrationEvent
                ?? throw new JsonException($"'{message.Type}' deserialized to null.");
        }
        catch (JsonException ex)
        {
            RecordFailure(message, ex.Message, ex);
            return;
        }

        var failures = new List<string>();
        foreach (Type handlerType in subscription.HandlerTypes)
        {
            try
            {
                await DeliverAsync(message, integrationEvent, handlerType, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failures.Add($"{handlerType.Name}: {ex.Message}");
                LogHandlerFailed(handlerType.FullName ?? handlerType.Name, message.Attempts + 1, ex);
            }
        }

        if (failures.Count == 0)
        {
            message.MarkProcessed(dateTime.UtcNow);
            return;
        }

        RecordFailure(message, string.Join(" | ", failures), exception: null);
    }

    private void RecordFailure(OutboxMessage message, string error, Exception? exception)
    {
        bool deadLettered = message.RecordFailure(error, dateTime.UtcNow, settings.MaxAttempts);
        if (deadLettered)
        {
            LogDeadLettered(message.Attempts, exception);
        }
    }

    /// <summary>One handler, one scope, one subscriber transaction - the same shape as TransactionBehavior.</summary>
    private async Task DeliverAsync(OutboxMessage message, IIntegrationEvent integrationEvent, Type handlerType, CancellationToken cancellationToken)
    {
        string handlerName = handlerType.FullName ?? handlerType.Name;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        // Parent = the publishing request's trace, so the delivery shows up in the same trace as the command.
        using Activity? activity = CrmDiagnostics.ActivitySource.StartActivity(
            $"outbox {integrationEvent.GetType().Name} -> {handlerType.Name}", ActivityKind.Consumer, message.TraceParent);
        activity?.SetTag("crm.outbox.message_id", message.Id);
        activity?.SetTag("crm.outbox.handler", handlerName);

        // The resolver works from the type's assembly: a handler lives in its module's Application project,
        // so this is the SUBSCRIBER's unit of work - AddModuleDbContext guarantees it is a ModuleDbContext.
        IUnitOfWork unitOfWork = services.GetRequiredService<IUnitOfWorkResolver>().ForRequest(handlerType);
        var subscriber = (ModuleDbContext)unitOfWork;

        if (await subscriber.HasConsumedAsync(message.Id, handlerName, cancellationToken))
        {
            LogAlreadyHandled(handlerName);
            return;
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var handler = (IIntegrationEventConsumer)services.GetRequiredService(handlerType);
            await handler.HandleAsync(integrationEvent, cancellationToken);

            // A subscriber may publish onward - into its own outbox, in this transaction.
            IReadOnlyList<IIntegrationEvent> onward = services.GetRequiredService<IPendingIntegrationEvents>().DequeueAll();
            if (onward.Count > 0)
            {
                unitOfWork.AddToOutbox(onward, correlation.CorrelationId);
            }

            subscriber.RecordConsumer(message.Id, handlerName, dateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            IReadOnlyCollection<IDomainEvent> domainEvents = unitOfWork.GetAndClearDomainEvents();
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            await services.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(domainEvents, cancellationToken);

            LogDelivered(handlerName);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch cycle failed")]
    private partial void LogCycleFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No subscriber for {EventType}; marked processed")]
    private partial void LogNoSubscriber(string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivered to {Handler}")]
    private partial void LogDelivered(string handler);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping {Handler}: already handled")]
    private partial void LogAlreadyHandled(string handler);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Handler} failed, attempt {Attempt}")]
    private partial void LogHandlerFailed(string handler, int attempt, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message dead-lettered after {Attempts} attempts")]
    private partial void LogDeadLettered(int attempts, Exception? exception);
}
