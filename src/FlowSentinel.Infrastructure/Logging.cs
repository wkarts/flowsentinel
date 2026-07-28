using Microsoft.Extensions.Logging;

namespace FlowSentinel.Infrastructure;

public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private DateOnly _currentDate;

    public RollingFileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) => new RollingFileLogger(this, categoryName);

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        lock (_sync)
        {
            EnsureWriter();
            _writer!.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {category}: {message}");
            if (exception is not null)
            {
                _writer.WriteLine(exception);
            }
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_writer is not null && _currentDate == today)
        {
            return;
        }

        _writer?.Dispose();
        _currentDate = today;
        var path = Path.Combine(_directory, $"flowsentinel-{today:yyyyMMdd}.log");
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private sealed class RollingFileLogger : ILogger
    {
        private readonly RollingFileLoggerProvider _provider;
        private readonly string _category;

        public RollingFileLogger(RollingFileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(logLevel, _category, formatter(state, exception), exception);
        }
    }
}

public static class LoggingExtensions
{
    public static ILoggingBuilder AddFlowSentinelFileLogging(this ILoggingBuilder builder, string directory)
    {
        builder.AddProvider(new RollingFileLoggerProvider(directory));
        return builder;
    }
}
