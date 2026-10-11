using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Outbox;

/// <summary>Drives the real dispatcher through <c>BackgroundService.StartAsync</c> and polls the store with a bounded
/// wait (the only place in the suite that waits). The outbox internals are not visible to the test assembly, and the
/// dispatcher is the public surface.</summary>
public sealed class OutboxDispatcherTests
{
    private static DateTimeOffset Past => Clock.Now.AddMinutes(-10);

    private static SampleIntegrationEvent Sample(string payload = "p", DateTimeOffset? at = null) =>
        new(Guid.NewGuid(), at ?? Past, payload);

    // --- delivery -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ADueMessageIsDeliveredOnceToItsSubscriberAndMarkedProcessed()
    {
        await using var harness = new OutboxHarness();
        var integrationEvent = Sample("hello");
        var message = OutboxHarness.Message(integrationEvent);
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
        }

        var handled = Assert.Single(harness.Probe.Handled);
        Assert.Equal(nameof(SampleConsumer), handled.Handler);
        Assert.Equal(integrationEvent, handled.Event);
        var stored = await harness.ReadAsync(message.Id);
        Assert.Equal(harness.Time.UtcNow, stored.ProcessedAtUtc);
        var consumer = Assert.Single(await harness.ConsumersAsync());
        Assert.Equal(message.Id, consumer.MessageId);
        Assert.Equal(typeof(SampleConsumer).FullName, consumer.Handler);
    }

    [Fact]
    public async Task AnAlreadyHandledMessageIsNotHandledAgainButIsStillMarkedProcessed()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(Sample());
        await harness.SeedAsync(message);
        await harness.SeedConsumerAsync(message.Id, typeof(SampleConsumer).FullName ?? string.Empty);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
        }

        Assert.Empty(harness.Probe.Handled);
        Assert.Single(await harness.ConsumersAsync());
    }

    [Fact]
    public async Task AMessageNobodySubscribesToIsMarkedProcessedWithoutAHandler()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(new NoSubscriberIntegrationEvent(Guid.NewGuid(), Past));
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
        }

        Assert.Empty(harness.Probe.Handled);
        Assert.Empty(await harness.ConsumersAsync());
    }

    [Fact]
    public async Task TheHandlerSeesTheMessagesCorrelationId()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(Sample(), "corr-from-publisher");
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
        }

        Assert.Equal("corr-from-publisher", Assert.Single(harness.Probe.Handled).CorrelationId);
    }

    [Fact]
    public async Task WithNoCorrelationIdTheHandlerSeesTheMessageIdInNFormat()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(Sample(), null);
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
        }

        Assert.Equal(message.Id.ToString("N"), Assert.Single(harness.Probe.Handled).CorrelationId);
    }

    // --- failures -----------------------------------------------------------------------------------------

    [Fact]
    public async Task InvalidJsonRecordsAFailureAndIsNotProcessed()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(Sample());
        await harness.SeedAsync(message);
        await using (var store = harness.Store())
        {
            var tracked = await store.Set<OutboxMessage>().SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken);
            store.Entry(tracked).Property(m => m.Content).CurrentValue = "{ this is not json";
            await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var running = await harness.StartAsync())
        {
            await running.UntilAttemptsAsync(message.Id, 1);
        }

        var stored = await harness.ReadAsync(message.Id);
        Assert.Equal(1, stored.Attempts);
        Assert.False(string.IsNullOrEmpty(stored.LastError));
        Assert.Null(stored.ProcessedAtUtc);
        Assert.Empty(harness.Probe.Handled);
    }

    [Fact]
    public async Task AFailingHandlerBacksOffNamesItselfInTheErrorAndLeavesNoConsumerRow()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(new FailingIntegrationEvent(Guid.NewGuid(), Past));
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilAttemptsAsync(message.Id, 1);
        }

        var stored = await harness.ReadAsync(message.Id);
        Assert.Equal(1, stored.Attempts);
        Assert.Equal(harness.Time.UtcNow.AddSeconds(2), stored.NextAttemptAtUtc);
        Assert.Contains("FailingConsumer: boom", stored.LastError, StringComparison.Ordinal);
        Assert.Null(stored.ProcessedAtUtc);
        Assert.Empty(await harness.ConsumersAsync());
        Assert.True(harness.Logger.Contains(LogLevel.Warning, "FailingConsumer"));
    }

    [Fact]
    public async Task RepeatedFailuresDeadLetterTheMessageAndItIsNeverPickedUpAgain()
    {
        await using var harness = new OutboxHarness(maxAttempts: 2);
        var message = OutboxHarness.Message(new FailingIntegrationEvent(Guid.NewGuid(), Past));
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilAttemptsAsync(message.Id, 1);
            harness.Time.Advance(TimeSpan.FromSeconds(3));
            await running.UntilAttemptsAsync(message.Id, 2);
            harness.Time.Advance(TimeSpan.FromHours(1));
            await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
        }

        var stored = await harness.ReadAsync(message.Id);
        Assert.Equal(2, stored.Attempts);
        Assert.Null(stored.NextAttemptAtUtc);
        Assert.Null(stored.ProcessedAtUtc);
        Assert.True(harness.Logger.Contains(LogLevel.Error, "dead-lettered after 2 attempts"));
    }

    [Fact]
    public async Task WhenOneOfTwoConsumersFailsTheMessageStaysPendingButTheSuccessfulConsumerIsRecorded()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(new MixedIntegrationEvent(Guid.NewGuid(), Past));
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilAttemptsAsync(message.Id, 1);
        }

        var stored = await harness.ReadAsync(message.Id);
        Assert.Null(stored.ProcessedAtUtc);
        Assert.Contains("MixedFailingConsumer: mixed boom", stored.LastError, StringComparison.Ordinal);
        var consumer = Assert.Single(await harness.ConsumersAsync());
        Assert.Equal(typeof(MixedOkConsumer).FullName, consumer.Handler);
        Assert.Equal(1, harness.Probe.Count(nameof(MixedOkConsumer)));
    }

    [Fact]
    public async Task OnRetryTheConsumerThatAlreadySucceededIsSkipped()
    {
        await using var harness = new OutboxHarness(maxAttempts: 5);
        var message = OutboxHarness.Message(new MixedIntegrationEvent(Guid.NewGuid(), Past));
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilAttemptsAsync(message.Id, 1);
            harness.Time.Advance(TimeSpan.FromSeconds(3));
            await running.UntilAttemptsAsync(message.Id, 2);
        }

        Assert.Equal(1, harness.Probe.Count(nameof(MixedOkConsumer)));
    }

    // --- selection ----------------------------------------------------------------------------------------

    [Fact]
    public async Task ANotYetDueMessageIsSkipped()
    {
        await using var harness = new OutboxHarness();
        var notDue = OutboxHarness.Message(Sample("later"));
        notDue.RecordFailure("earlier failure", harness.Time.UtcNow, 5);
        var due = OutboxHarness.Message(Sample("now"));
        await harness.SeedAsync(notDue, due);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(due.Id);
        }

        var stored = await harness.ReadAsync(notDue.Id);
        Assert.Equal(1, stored.Attempts);
        Assert.Null(stored.ProcessedAtUtc);
        Assert.Equal("now", Assert.Single(harness.Probe.Handled).Event is SampleIntegrationEvent e ? e.Payload : null);
    }

    [Fact]
    public async Task AnAlreadyProcessedMessageIsSkipped()
    {
        await using var harness = new OutboxHarness();
        var processed = OutboxHarness.Message(Sample("old"));
        processed.MarkProcessed(Past);
        var due = OutboxHarness.Message(Sample("new"));
        await harness.SeedAsync(processed, due);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(due.Id);
        }

        Assert.Single(harness.Probe.Handled);
        Assert.Equal(Past, (await harness.ReadAsync(processed.Id)).ProcessedAtUtc);
    }

    [Fact]
    public async Task EachCycleDeliversTheOldestMessagesFirstUpToTheBatchSize()
    {
        await using var harness = new OutboxHarness(batchSize: 2, pollingInterval: TimeSpan.FromHours(1));
        var newest = OutboxHarness.Message(Sample("newest", Clock.Now.AddMinutes(-1)));
        var oldest = OutboxHarness.Message(Sample("oldest", Clock.Now.AddMinutes(-30)));
        var middle = OutboxHarness.Message(Sample("middle", Clock.Now.AddMinutes(-10)));
        await harness.SeedAsync(newest, oldest, middle);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(oldest.Id);
            await running.UntilProcessedAsync(middle.Id);
        }

        Assert.Null((await harness.ReadAsync(newest.Id)).ProcessedAtUtc);
        Assert.Equal(
            ["oldest", "middle"],
            harness.Probe.Handled.Select(h => ((SampleIntegrationEvent)h.Event).Payload));
    }

    // --- onward publishing and domain events --------------------------------------------------------------

    [Fact]
    public async Task AConsumerThatPublishesOnwardWritesANewOutboxRowWithTheDispatchingCorrelationId()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(new PublishingIntegrationEvent(Guid.NewGuid(), Past), "corr-onward");
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
            await running.UntilAsync(async h => (await h.ReadAllAsync()).Any(m => m.Type == typeof(SecondIntegrationEvent).FullName));
        }

        var onward = Assert.Single(await harness.ReadAllAsync(), m => m.Type == typeof(SecondIntegrationEvent).FullName);
        Assert.Equal("corr-onward", onward.CorrelationId);
    }

    [Fact]
    public async Task DomainEventsRaisedByAConsumerAreDispatchedAfterItsCommit()
    {
        await using var harness = new OutboxHarness();
        var message = OutboxHarness.Message(new DomainEventTriggerIntegrationEvent(Guid.NewGuid(), Past));
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
        }

        await harness.DomainEvents.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyCollection<IDomainEvent>>(events => events.Count == 1 && events.All(e => e is SampleDomainEvent)),
            Arg.Any<CancellationToken>());
    }

    // --- resilience ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AFailingCycleIsLoggedAndTheLoopKeepsRunning()
    {
        await using var harness = new OutboxHarness(withBrokenModule: true);
        var message = OutboxHarness.Message(Sample());
        await harness.SeedAsync(message);

        await using (var running = await harness.StartAsync())
        {
            await running.UntilProcessedAsync(message.Id);
            await running.UntilErrorsLoggedAsync(2);
        }

        Assert.True(harness.Logger.Entries.Count(e => e.Message == "Outbox dispatch cycle failed") >= 2);
    }

    [Fact]
    public async Task StoppingTheDispatcherEndsTheLoopWithoutAFault()
    {
        await using var harness = new OutboxHarness();

        await using (await harness.StartAsync())
        {
            Assert.NotNull(harness.Dispatcher.ExecuteTask);
        }

        var task = Assert.IsAssignableFrom<Task>(harness.Dispatcher.ExecuteTask);
        // Completed or cancelled (cancellation can land inside a dispatch cycle), never faulted.
        Assert.True(task.IsCompleted);
        Assert.False(task.IsFaulted);
    }

    [Fact]
    public async Task AnEmptyOutboxDoesNothing()
    {
        await using var harness = new OutboxHarness();

        await using (var running = await harness.StartAsync())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
            await running.UntilAsync(_ => Task.FromResult(true));
        }

        Assert.Empty(harness.Probe.Handled);
        Assert.DoesNotContain(harness.Logger.Entries, e => e.Level >= LogLevel.Warning);
    }
}
