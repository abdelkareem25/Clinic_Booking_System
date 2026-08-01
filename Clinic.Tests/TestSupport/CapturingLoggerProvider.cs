using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Clinic.Tests.TestSupport
{
    /// <summary>
    /// Captures log output so tests can assert on what was written - and, more importantly for a
    /// clinical system, on what was NOT.
    /// </summary>
    public sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception);

        private readonly ConcurrentQueue<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries.ToList();

        /// <summary>Everything written, as one blob - convenient for "must not contain" assertions.</summary>
        public string AllText => string.Join(Environment.NewLine, _entries.Select(e => e.Message));

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly string _category;
            private readonly ConcurrentQueue<Entry> _entries;

            public CapturingLogger(string category, ConcurrentQueue<Entry> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _entries.Enqueue(new Entry(_category, logLevel, formatter(state, exception), exception));
            }
        }
    }
}
