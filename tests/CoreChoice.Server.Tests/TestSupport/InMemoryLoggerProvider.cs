using Microsoft.Extensions.Logging;

namespace CoreChoice.Server.Tests;

/// <summary>
/// Captures every formatted log message written by the host, so a test can assert on what actually
/// reached a log sink — not just what a column allowlist can see. The 502 branch logs a warning
/// carrying the persona and prompt version; if anyone ever added the dilemma to that template, this
/// is the only kind of test that would notice, because it would ship straight to the VPS's
/// container logs without ever touching the database.
/// </summary>
internal sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_gate) return [.. _messages];
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private void Record(string message)
    {
        lock (_gate) _messages.Add(message);
    }

    private sealed class CapturingLogger(InMemoryLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            owner.Record(formatter(state, exception));
            if (exception is not null) owner.Record(exception.ToString());
        }
    }
}
