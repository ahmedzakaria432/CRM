using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Support;

public sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

/// <summary>Captures what a class logged. An NSubstitute <see cref="ILogger"/> cannot read the state of a
/// <c>[LoggerMessage]</c> call reliably; this formats it the way a real provider would.</summary>
public sealed class RecordingLogger<T>(LogLevel minimumLevel = LogLevel.Trace) : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _gate = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        lock (_gate)
        {
            _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
        }
    }

    public bool Contains(LogLevel level, string text) =>
        Entries.Any(e => e.Level == level && e.Message.Contains(text, StringComparison.Ordinal));
}
