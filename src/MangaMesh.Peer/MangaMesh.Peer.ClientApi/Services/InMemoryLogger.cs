using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }

    public class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _logs = new();
        private readonly int _maxLogs = 1000;

        public ILogger CreateLogger(string categoryName)
        {
            return new InMemoryLogger(categoryName, this);
        }

        public void AddLog(LogEntry entry)
        {
            _logs.Enqueue(entry);
            while (_logs.Count > _maxLogs)
            {
                _logs.TryDequeue(out _);
            }
        }

        public IEnumerable<LogEntry> GetLogs()
        {
            return _logs.ToArray();
        }

        public void Clear()
        {
            _logs.Clear();
        }

        public void Dispose() { }
    }

    public class InMemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly InMemoryLoggerProvider _provider;

        public InMemoryLogger(string categoryName, InMemoryLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel,
                Category = _categoryName,
                Message = formatter(state, exception),
                Exception = exception?.ToString()
            };

            _provider.AddLog(entry);
        }
    }
}
