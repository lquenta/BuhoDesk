using System.Collections.Concurrent;
using System.IO;

namespace BuhoShared.Services;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string? StackTrace { get; set; }
}

public interface ILogger
{
    void Log(LogLevel level, string category, string message, Exception? exception = null);
    void Debug(string category, string message);
    void Info(string category, string message);
    void Warning(string category, string message, Exception? exception = null);
    void Error(string category, string message, Exception? exception = null);
    void Critical(string category, string message, Exception? exception = null);
    IEnumerable<LogEntry> GetLogs();
    void ClearLogs();
    event EventHandler<LogEntry>? LogEntryAdded;
}

public class Logger : ILogger
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private readonly object _lockObject = new();
    private const int MAX_LOG_ENTRIES = 1000;

    public event EventHandler<LogEntry>? LogEntryAdded;

    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Exception = exception,
            StackTrace = exception?.StackTrace
        };

        _logs.Enqueue(entry);

        // Keep only the last MAX_LOG_ENTRIES
        while (_logs.Count > MAX_LOG_ENTRIES)
        {
            _logs.TryDequeue(out _);
        }

        // Write to console with color coding
        WriteToConsole(entry);

        // Notify subscribers
        LogEntryAdded?.Invoke(this, entry);
    }

    public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
    public void Info(string category, string message) => Log(LogLevel.Info, category, message);
    public void Warning(string category, string message, Exception? exception = null) => Log(LogLevel.Warning, category, message, exception);
    public void Error(string category, string message, Exception? exception = null) => Log(LogLevel.Error, category, message, exception);
    public void Critical(string category, string message, Exception? exception = null) => Log(LogLevel.Critical, category, message, exception);

    public IEnumerable<LogEntry> GetLogs()
    {
        return _logs.ToArray();
    }

    public void ClearLogs()
    {
        lock (_lockObject)
        {
            _logs.Clear();
        }
    }

    private void WriteToConsole(LogEntry entry)
    {
        var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
        var level = entry.Level.ToString().ToUpper().PadRight(8);
        var category = entry.Category.PadRight(15);
        
        var logMessage = $"[{timestamp}] [{level}] [{category}] {entry.Message}";
        
        // Write to console with color coding
        var originalColor = Console.ForegroundColor;
        
        try
        {
            // Set color based on log level
            Console.ForegroundColor = entry.Level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };

            Console.WriteLine(logMessage);
            
            if (entry.Exception != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"    Exception: {entry.Exception.Message}");
                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    Console.WriteLine($"    StackTrace: {entry.Exception.StackTrace}");
                }
            }
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
        
        // Also write to file
        WriteToFile(logMessage, entry);
    }
    
    private void WriteToFile(string logMessage, LogEntry entry)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BuhoClone", "Logs");
            Directory.CreateDirectory(logDir);
            
            // Determine if this is server or client based on the process name
            var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            var logFile = Path.Combine(logDir, $"{processName}_{DateTime.Now:yyyy-MM-dd}.log");
            
            var fullMessage = logMessage;
            if (entry.Exception != null)
            {
                fullMessage += $"\n    Exception: {entry.Exception.Message}";
                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    fullMessage += $"\n    StackTrace: {entry.Exception.StackTrace}";
                }
            }
            
            File.AppendAllText(logFile, fullMessage + Environment.NewLine);
        }
        catch
        {
            // Ignore file logging errors
        }
    }
}

public static class LoggerExtensions
{
    public static void LogPerformance(this ILogger logger, string category, string operation, TimeSpan duration)
    {
        logger.Info(category, $"Performance: {operation} completed in {duration.TotalMilliseconds:F2}ms");
    }

    public static void LogNetworkEvent(this ILogger logger, string category, string event_, string details = "")
    {
        logger.Info(category, $"Network: {event_} {details}");
    }

    public static void LogConnectionEvent(this ILogger logger, string category, string clientId, string event_)
    {
        logger.Info(category, $"Connection: Client {clientId} {event_}");
    }
}
