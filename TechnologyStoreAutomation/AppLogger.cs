using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace TechnologyStoreAutomation;

/// <summary>
/// Centralized logging factory for the application.
/// Provides consistent logging configuration across all components.
/// </summary>
public static class AppLogger
{
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Gets or initializes the logger factory with console logging
    /// </summary>
    public static ILoggerFactory Factory => _loggerFactory ??= CreateLoggerFactory();

    private static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                    options.SingleLine = true;
                });
        });
    }

    /// <summary>
    /// Creates a logger for the specified type
    /// </summary>
    public static ILogger<T> CreateLogger<T>() => Factory.CreateLogger<T>();

    /// <summary>
    /// Creates a logger with the specified category name
    /// </summary>
    public static ILogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);
}

