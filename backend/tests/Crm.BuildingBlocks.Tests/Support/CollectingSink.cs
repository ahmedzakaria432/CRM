using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>Collects Serilog events. Tests build their own local <see cref="Logger"/>; the global
/// <c>Log.Logger</c> is never touched.</summary>
public sealed class CollectingSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events => _events;

    public LogEvent Only => Assert.Single(_events);

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        _events.Add(logEvent);
    }

    public Logger Logger(Action<LoggerConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var configuration = new LoggerConfiguration();
        configure(configuration);
        return configuration.WriteTo.Sink(this).CreateLogger();
    }

    public static string? Scalar(LogEvent logEvent, string property)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        return logEvent.Properties.TryGetValue(property, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;
    }
}
