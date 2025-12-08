using Microsoft.Extensions.Logging;

namespace TechnologyStoreAutomation;

/// <summary>
/// Centralized global exception handler for the application.
/// Catches unhandled exceptions from all sources and provides consistent error handling.
/// </summary>
public static class GlobalExceptionHandler
{
    private static ILogger? _logger;
    
    /// <summary>
    /// Initializes the global exception handler with the application's logger
    /// </summary>
    public static void Initialize(ILogger? logger = null)
    {
        _logger = logger ?? AppLogger.CreateLogger("GlobalExceptionHandler");
        
        // Handle exceptions on the UI thread
        Application.ThreadException += OnThreadException;
        
        // Handle exceptions on non-UI threads
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        
        // Set unhandled exception mode
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        
        // Handle task scheduler exceptions (for async/await exceptions that aren't observed)
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        
        _logger.LogInformation("Global exception handler initialized");
    }

    /// <summary>
    /// Handles exceptions that occur on the main UI thread
    /// </summary>
    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        HandleException(e.Exception, "UI Thread Exception");
    }

    /// <summary>
    /// Handles exceptions that occur on non-UI threads
    /// </summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception("Unknown error occurred");
        
        HandleException(exception, "Unhandled Exception", e.IsTerminating);
        
        if (e.IsTerminating)
        {
            _logger?.LogCritical(exception, "Application is terminating due to unhandled exception");
        }
    }

    /// <summary>
    /// Handles exceptions from unobserved tasks (async operations without await)
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Mark the exception as observed to prevent the process from terminating
        e.SetObserved();
        
        HandleException(e.Exception, "Unobserved Task Exception");
    }

    /// <summary>
    /// Central exception handling logic
    /// </summary>
    private static void HandleException(Exception exception, string source, bool isTerminating = false)
    {
        try
        {
            // Log the exception with full details
            _logger?.LogError(exception, 
                "Unhandled exception from {Source}: {Message}", 
                source, 
                exception.Message);

            // Get user-friendly message
            var userMessage = GetUserFriendlyMessage(exception);
            var title = isTerminating ? "Critical Error" : "Application Error";
            var icon = isTerminating ? MessageBoxIcon.Error : MessageBoxIcon.Warning;

            // Show user-friendly dialog
            var result = MessageBox.Show(
                $"{userMessage}\n\n" +
                $"Technical Details:\n{exception.GetType().Name}: {exception.Message}\n\n" +
                (isTerminating 
                    ? "The application will now close." 
                    : "Would you like to continue using the application?"),
                title,
                isTerminating ? MessageBoxButtons.OK : MessageBoxButtons.YesNo,
                icon);

            // If user chooses No on non-terminating exception, exit gracefully
            if (!isTerminating && result == DialogResult.No)
            {
                _logger?.LogInformation("User chose to exit application after exception");
                Application.Exit();
            }
        }
        catch (Exception handlerException)
        {
            // Last resort if exception handler itself fails
            try
            {
                _logger?.LogCritical(handlerException, "Exception handler failed");
                MessageBox.Show(
                    $"A critical error occurred and the error handler failed.\n\n{exception.Message}",
                    "Critical Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // Silently fail if even the message box fails
            }
        }
    }

    /// <summary>
    /// Converts technical exceptions to user-friendly messages
    /// </summary>
    private static string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            // Database errors
            Npgsql.NpgsqlException => 
                "A database connection error occurred. Please check your network connection and database configuration.",
            
            // Network errors
            HttpRequestException => 
                "A network error occurred. Please check your internet connection.",
            
            TimeoutException => 
                "The operation timed out. Please try again.",
            
            // Invalid operation
            InvalidOperationException ex when ex.Message.Contains("connection") => 
                "Unable to connect to the database. Please verify your configuration.",
            
            InvalidOperationException => 
                "An invalid operation was attempted. Please try again or restart the application.",
            
            // Argument errors (usually bugs)
            ArgumentException or ArgumentNullException => 
                "An unexpected error occurred due to invalid data. Please restart the application.",
            
            // Task cancellation
            TaskCanceledException or OperationCanceledException => 
                "The operation was cancelled.",
            
            // Out of memory
            OutOfMemoryException => 
                "The application ran out of memory. Please close other applications and try again.",
            
            // Aggregate exceptions (unwrap)
            AggregateException ae => 
                GetUserFriendlyMessage(ae.InnerException ?? ae),
            
            // Default
            _ => "An unexpected error occurred. The application may not function correctly."
        };
    }

    /// <summary>
    /// Manually report an exception (for caught exceptions that should still be logged)
    /// </summary>
    public static void ReportException(Exception exception, string context = "")
    {
        var source = string.IsNullOrEmpty(context) ? "Reported Exception" : $"Reported: {context}";
        _logger?.LogError(exception, "{Source}: {Message}", source, exception.Message);
    }

    /// <summary>
    /// Safely executes an action with exception handling
    /// </summary>
    public static void SafeExecute(Action action, string context = "")
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
            HandleException(ex, context);
        }
    }

    /// <summary>
    /// Safely executes a function and returns a default value on failure
    /// </summary>
    public static T SafeExecute<T>(Func<T> func, T defaultValue, string context = "")
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
            return defaultValue;
        }
    }

    /// <summary>
    /// Safely executes an async action with exception handling
    /// </summary>
    public static async Task SafeExecuteAsync(Func<Task> action, string context = "")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
            HandleException(ex, context);
        }
    }
}

