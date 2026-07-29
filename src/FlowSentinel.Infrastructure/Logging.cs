using Microsoft.Extensions.Logging;

namespace FlowSentinel.Infrastructure;

public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private static readonly TimeSpan InformationFlushInterval = TimeSpan.FromSeconds(2);

    private readonly string _directory;
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private DateOnly _currentDate;
    private DateTimeOffset _lastFlushAt = DateTimeOffset.MinValue;

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

            var now = DateTimeOffset.UtcNow;
            if (level >= LogLevel.Warning || now - _lastFlushAt >= InformationFlushInterval)
            {
                _writer.Flush();
                _lastFlushAt = now;
            }
        }
    }

    private void EnsureWriter()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_writer is not null && _currentDate == today)
        {
            return;
        }

        _writer?.Flush();
        _writer?.Dispose();
        _currentDate = today;
        var path = Path.Combine(_directory, $"flowsentinel-{today:yyyyMMdd}.log");
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = false
        };
        _lastFlushAt = DateTimeOffset.UtcNow;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Flush();
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
        builder.AddFilter<RollingFileLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.AddFilter<RollingFileLoggerProvider>("Microsoft.Extensions.Http", LogLevel.Warning);
        builder.AddFilter<RollingFileLoggerProvider>("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.AddProvider(new RollingFileLoggerProvider(directory));
        return builder;
    }
}
