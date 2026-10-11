using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Diagnostics;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Application.Behaviors;

public sealed class LoggingBehaviorTests
{
    private sealed record Measurement(double Value, IReadOnlyDictionary<string, object?> Tags);

    /// <summary>Listens to the application's meter and activity source. Both are process-global, so everything is
    /// filtered by the request's name and the listeners are disposed with the test.</summary>
    private sealed class Capture : IDisposable
    {
        private readonly MeterListener _meter = new();
        private readonly ActivityListener _activities;

        public Capture(string requestName)
        {
            _meter.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == CrmDiagnostics.SourceName && instrument.Name == "crm.request.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _meter.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            {
                var dictionary = new Dictionary<string, object?>();
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    dictionary[tag.Key] = tag.Value;
                }

                if (Equals(dictionary.GetValueOrDefault("crm.request.name"), requestName))
                {
                    Measurements.Add(new Measurement(value, dictionary));
                }
            });
            _meter.Start();

            _activities = new ActivityListener
            {
                ShouldListenTo = source => source.Name == CrmDiagnostics.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == requestName)
                    {
                        Activities.Add(activity);
                    }
                },
            };
            ActivitySource.AddActivityListener(_activities);
        }

        public ConcurrentBag<Measurement> Measurements { get; } = [];

        public ConcurrentBag<Activity> Activities { get; } = [];

        public void Dispose()
        {
            _meter.Dispose();
            _activities.Dispose();
        }
    }

    [Fact]
    public async Task ASuccessfulQueryLogsHandlingAndHandled()
    {
        var logger = new RecordingLogger<LoggingBehavior<ReadQuery, Result<int>>>(LogLevel.Information);
        var behavior = new LoggingBehavior<ReadQuery, Result<int>>(logger);

        var result = await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(LogLevel.Information, logger.Entries[0].Level);
        Assert.Equal("Handling ReadQuery", logger.Entries[0].Message);
        Assert.Equal(LogLevel.Information, logger.Entries[1].Level);
        Assert.StartsWith("Handled ReadQuery in ", logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASuccessRecordsTheDurationMetricAsAQuery()
    {
        using var capture = new Capture(nameof(ReadQuery));
        var behavior = new LoggingBehavior<ReadQuery, Result<int>>(new RecordingLogger<LoggingBehavior<ReadQuery, Result<int>>>());

        await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        var measurement = Assert.Single(capture.Measurements);
        Assert.True(measurement.Value >= 0);
        Assert.Equal("success", measurement.Tags["crm.outcome"]);
        Assert.Equal("query", measurement.Tags["crm.request.kind"]);
        Assert.Equal(nameof(ReadQuery), measurement.Tags["crm.request.name"]);
    }

    [Fact]
    public async Task ACommandIsTaggedAsACommand()
    {
        using var capture = new Capture(nameof(WriteCommand));
        var behavior = new LoggingBehavior<WriteCommand, Result>(new RecordingLogger<LoggingBehavior<WriteCommand, Result>>());

        await behavior.Handle(new WriteCommand(), new NextProbe<Result>(Result.Success()).Next, TestContext.Current.CancellationToken);

        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal("command", measurement.Tags["crm.request.kind"]);
        var activity = Assert.Single(capture.Activities);
        Assert.Equal("command", activity.GetTagItem("crm.request.kind"));
        Assert.Equal(nameof(WriteCommand), activity.GetTagItem("crm.request.name"));
    }

    [Fact]
    public async Task AFailedResultIsAWarningWithTheErrorCodeAndAnErrorSpan()
    {
        using var capture = new Capture(nameof(ReadQuery));
        var logger = new RecordingLogger<LoggingBehavior<ReadQuery, Result<int>>>();
        var behavior = new LoggingBehavior<ReadQuery, Result<int>>(logger);
        var failure = Result.Failure<int>(Error.NotFound("Thing.NotFound", "no"));

        var result = await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(failure).Next, TestContext.Current.CancellationToken);

        Assert.Same(failure, result);
        Assert.True(logger.Contains(LogLevel.Warning, "Thing.NotFound"));
        Assert.Equal("failure", Assert.Single(capture.Measurements).Tags["crm.outcome"]);
        var activity = Assert.Single(capture.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Thing.NotFound", activity.StatusDescription);
        Assert.Equal("Thing.NotFound", activity.GetTagItem("crm.error.code"));
    }

    [Fact]
    public async Task AnExceptionIsRethrownRecordedAndLoggedAsAWarning()
    {
        using var capture = new Capture(nameof(WriteCommand));
        var logger = new RecordingLogger<LoggingBehavior<WriteCommand, Result>>();
        var behavior = new LoggingBehavior<WriteCommand, Result>(logger);
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new WriteCommand(), NextProbe.Throwing<Result>(boom).Next, TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
        Assert.True(logger.Contains(LogLevel.Warning, "WriteCommand failed after"));
        Assert.Equal("exception", Assert.Single(capture.Measurements).Tags["crm.outcome"]);
        var activity = Assert.Single(capture.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(nameof(InvalidOperationException), activity.StatusDescription);
        Assert.Contains(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task ThePayloadLineIsLoggedOnlyWhenDebugIsEnabled()
    {
        var quiet = new RecordingLogger<LoggingBehavior<ReadQuery, Result<int>>>(LogLevel.Information);
        var verbose = new RecordingLogger<LoggingBehavior<ReadQuery, Result<int>>>(LogLevel.Debug);

        await new LoggingBehavior<ReadQuery, Result<int>>(quiet)
            .Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);
        await new LoggingBehavior<ReadQuery, Result<int>>(verbose)
            .Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(quiet.Entries, e => e.Level == LogLevel.Debug);
        Assert.Single(verbose.Entries, e => e.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task TheSpanIsNamedAfterTheRequestAndEndsWhenTheHandlerReturns()
    {
        using var capture = new Capture(nameof(ReadQuery));
        var behavior = new LoggingBehavior<ReadQuery, Result<int>>(new RecordingLogger<LoggingBehavior<ReadQuery, Result<int>>>());

        await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        var activity = Assert.Single(capture.Activities);
        Assert.Equal(nameof(ReadQuery), activity.OperationName);
        Assert.True(activity.IsStopped);
        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task ThePlainResponseOfANonResultRequestCountsAsSuccess()
    {
        using var capture = new Capture(nameof(NonResultRequest));
        var behavior = new LoggingBehavior<NonResultRequest, string>(new RecordingLogger<LoggingBehavior<NonResultRequest, string>>());

        var response = await behavior.Handle(new NonResultRequest(), new NextProbe<string>("text").Next, TestContext.Current.CancellationToken);

        Assert.Equal("text", response);
        Assert.Equal("success", Assert.Single(capture.Measurements).Tags["crm.outcome"]);
    }

    [Fact]
    public void TheInstrumentsLiveOnTheCrmMeterAndSource()
    {
        Assert.Equal("Crm", CrmDiagnostics.SourceName);
        Assert.Equal("Crm", CrmDiagnostics.Meter.Name);
        Assert.Equal("Crm", CrmDiagnostics.ActivitySource.Name);
        Assert.Equal("crm.request.duration", CrmDiagnostics.RequestDuration.Name);
        Assert.Equal("ms", CrmDiagnostics.RequestDuration.Unit);
    }
}
