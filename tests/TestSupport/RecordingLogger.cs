using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.TestSupport;

// Records each log entry so a test can assert on what was logged and at what level.
public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, formatter(state, exception)));

    public sealed record LogEntry(LogLevel Level, string Message);
}
