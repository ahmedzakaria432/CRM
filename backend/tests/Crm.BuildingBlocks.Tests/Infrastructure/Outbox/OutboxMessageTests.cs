using System.Text.Json;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Outbox;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    private static OutboxMessage NewMessage(string payload = "hello") =>
        OutboxMessage.From(new SampleIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(-5), payload), "corr-1", "00-trace-span-01");

    // --- From ---------------------------------------------------------------------------------------------

    [Fact]
    public void FromCopiesTheEventsIdentityAndTime()
    {
        var integrationEvent = new SampleIntegrationEvent(Guid.NewGuid(), Now.AddMinutes(-5), "x");

        var message = OutboxMessage.From(integrationEvent, "corr-1", "00-trace-span-01");

        Assert.Equal(integrationEvent.Id, message.Id);
        Assert.Equal(integrationEvent.OccurredOnUtc, message.OccurredOnUtc);
    }

    [Fact]
    public void FromStoresTheEventTypesFullNameAsTheType()
    {
        Assert.Equal(typeof(SampleIntegrationEvent).FullName, NewMessage().Type);
    }

    [Fact]
    public void FromSerialisesTheEventAsCamelCaseJson()
    {
        var message = NewMessage("the-payload");

        using var json = JsonDocument.Parse(message.Content);
        Assert.Equal("the-payload", json.RootElement.GetProperty("payload").GetString());
        Assert.True(json.RootElement.TryGetProperty("id", out _));
        Assert.True(json.RootElement.TryGetProperty("occurredOnUtc", out _));
        Assert.False(json.RootElement.TryGetProperty("Payload", out _));
    }

    [Fact]
    public void FromStoresTheCorrelationIdAndTraceParent()
    {
        var message = NewMessage();

        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Equal("00-trace-span-01", message.TraceParent);
    }

    [Fact]
    public void FromAcceptsNoCorrelationOrTraceParent()
    {
        var message = OutboxMessage.From(new SampleIntegrationEvent(Guid.NewGuid(), Now, "x"), null, null);

        Assert.Null(message.CorrelationId);
        Assert.Null(message.TraceParent);
    }

    [Fact]
    public void ANewMessageIsUndeliveredWithNoAttempts()
    {
        var message = NewMessage();

        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.LastError);
    }

    [Fact]
    public void FromRejectsANullEvent()
    {
        Func<IIntegrationEvent, string?, string?, OutboxMessage> method = OutboxMessage.From;

        NullArguments.AssertThrowsArgumentNull(method, null, "corr", null);
    }

    [Fact]
    public void TheContentRoundTripsToTheSameEvent()
    {
        var original = new SampleIntegrationEvent(Guid.NewGuid(), Now, "round-trip");
        var message = OutboxMessage.From(original, null, null);

        var back = JsonSerializer.Deserialize<SampleIntegrationEvent>(message.Content, JsonSerializerOptions.Web);

        Assert.Equal(original, back);
    }

    // --- MarkProcessed ------------------------------------------------------------------------------------

    [Fact]
    public void MarkProcessedSetsTheTimestampAndClearsTheNextAttempt()
    {
        var message = NewMessage();
        message.RecordFailure("boom", Now, 5);
        Assert.NotNull(message.NextAttemptAtUtc);

        message.MarkProcessed(Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), message.ProcessedAtUtc);
        Assert.Null(message.NextAttemptAtUtc);
    }

    // --- RecordFailure ------------------------------------------------------------------------------------

    [Fact]
    public void ARecordedFailureIncrementsAttemptsAndKeepsTheError()
    {
        var message = NewMessage();

        var deadLettered = message.RecordFailure("it broke", Now, 5);

        Assert.False(deadLettered);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("it broke", message.LastError);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    [InlineData(8, 256)]
    public void TheNextAttemptBacksOffExponentially(int failures, int expectedSeconds)
    {
        var message = NewMessage();

        for (var i = 0; i < failures; i++)
        {
            message.RecordFailure("e", Now, 100);
        }

        Assert.Equal(Now.AddSeconds(expectedSeconds), message.NextAttemptAtUtc);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(30)]
    public void TheBackoffIsCappedAtThreeHundredSeconds(int failures)
    {
        var message = NewMessage();

        for (var i = 0; i < failures; i++)
        {
            message.RecordFailure("e", Now, 100);
        }

        Assert.Equal(Now.AddSeconds(300), message.NextAttemptAtUtc);
    }

    [Fact]
    public void ReachingTheMaximumDeadLettersTheMessage()
    {
        var message = NewMessage();

        Assert.False(message.RecordFailure("e", Now, 3));
        Assert.False(message.RecordFailure("e", Now, 3));
        var deadLettered = message.RecordFailure("e", Now, 3);

        Assert.True(deadLettered);
        Assert.Equal(3, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public void AMaximumOfOneDeadLettersOnTheFirstFailure()
    {
        var message = NewMessage();

        Assert.True(message.RecordFailure("e", Now, 1));
        Assert.Null(message.NextAttemptAtUtc);
    }

    [Fact]
    public void ALongErrorIsTruncatedToExactlyTheMaximumLength()
    {
        var message = NewMessage();
        var error = new string('x', 3000);

        message.RecordFailure(error, Now, 5);

        Assert.Equal(OutboxMessage.MaxErrorLength, message.LastError?.Length);
        Assert.Equal(2000, OutboxMessage.MaxErrorLength);
        Assert.Equal(error[..2000], message.LastError);
    }

    [Fact]
    public void AnErrorAtTheMaximumLengthIsKeptWhole()
    {
        var message = NewMessage();
        var error = new string('y', OutboxMessage.MaxErrorLength);

        message.RecordFailure(error, Now, 5);

        Assert.Equal(error, message.LastError);
    }

    [Fact]
    public void RecordFailureRejectsANullError()
    {
        var message = NewMessage();
        Func<string, DateTimeOffset, int, bool> method = message.RecordFailure;

        NullArguments.AssertThrowsArgumentNull(method, null, Now, 5);
    }

    // --- consumer record ----------------------------------------------------------------------------------

    [Fact]
    public void AConsumerRecordKeepsItsValues()
    {
        var id = Guid.NewGuid();

        var consumer = new OutboxMessageConsumer(id, "Some.Handler", Now);

        Assert.Equal(id, consumer.MessageId);
        Assert.Equal("Some.Handler", consumer.Handler);
        Assert.Equal(Now, consumer.ProcessedAtUtc);
    }
}
