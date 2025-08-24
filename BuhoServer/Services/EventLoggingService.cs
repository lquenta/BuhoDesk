using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BuhoShared.Models;
using BuhoShared.Services;
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Where

namespace BuhoServer.Services;

/// <summary>
/// Service for logging media detection events to files
/// </summary>
public class EventLoggingService : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _logDirectory;
    private readonly string _screenshotsDirectory;
    private readonly object _logLock = new();

    public EventLoggingService(ILogger logger)
    {
        _logger = logger;
        
        // Create directories in the application folder
        var appFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        _logDirectory = Path.Combine(appFolder, "MediaDetectionLogs");
        _screenshotsDirectory = Path.Combine(_logDirectory, "Screenshots");
        
        // Ensure directories exist
        Directory.CreateDirectory(_logDirectory);
        Directory.CreateDirectory(_screenshotsDirectory);
        
        _logger.Info("EventLogging", $"Event logging initialized. Log directory: {_logDirectory}");
    }

    /// <summary>
    /// Logs a media detection event with optional screenshot
    /// </summary>
    public async Task LogMediaEventAsync(MediaDetectionEvent mediaEvent)
    {
        try
        {
            // Create log entry
            var logEntry = new MediaDetectionLogEntry
            {
                Timestamp = mediaEvent.Timestamp,
                EventType = mediaEvent.EventType,
                Url = mediaEvent.Url,
                Title = mediaEvent.Title,
                Confidence = mediaEvent.Confidence,
                WindowTitle = mediaEvent.WindowTitle,
                ProcessName = mediaEvent.ProcessName,
                ScreenshotFileName = await SaveScreenshotAsync(mediaEvent)
            };

            // Save to JSON log file
            await SaveToLogFileAsync(logEntry);
            
            // Save to CSV for easy analysis
            await SaveToCsvFileAsync(logEntry);
            
            _logger.Info("EventLogging", $"Media event logged: {mediaEvent.EventType} - {mediaEvent.Title}");
        }
        catch (Exception ex)
        {
            _logger.Error("EventLogging", $"Error logging media event: {ex.Message}");
        }
    }

    private async Task<string?> SaveScreenshotAsync(MediaDetectionEvent mediaEvent)
    {
        if (mediaEvent.Screenshot.Length == 0)
            return null;

        try
        {
            var timestamp = mediaEvent.Timestamp.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{mediaEvent.EventType}_{timestamp}.jpg";
            var filePath = Path.Combine(_screenshotsDirectory, fileName);
            
            await File.WriteAllBytesAsync(filePath, mediaEvent.Screenshot);
            
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.Error("EventLogging", $"Error saving screenshot: {ex.Message}");
            return null;
        }
    }

    private async Task SaveToLogFileAsync(MediaDetectionLogEntry logEntry)
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var logFilePath = Path.Combine(_logDirectory, $"media_events_{date}.json");
        
        lock (_logLock)
        {
            var logEntries = new List<MediaDetectionLogEntry>();
            
            // Read existing entries if file exists
            if (File.Exists(logFilePath))
            {
                try
                {
                    var existingJson = File.ReadAllText(logFilePath);
                    logEntries = JsonSerializer.Deserialize<List<MediaDetectionLogEntry>>(existingJson) ?? new List<MediaDetectionLogEntry>();
                }
                catch
                {
                    logEntries = new List<MediaDetectionLogEntry>();
                }
            }
            
            // Add new entry
            logEntries.Add(logEntry);
            
            // Write back to file
            var json = JsonSerializer.Serialize(logEntries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(logFilePath, json);
        }
    }

    private async Task SaveToCsvFileAsync(MediaDetectionLogEntry logEntry)
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var csvFilePath = Path.Combine(_logDirectory, $"media_events_{date}.csv");
        
        lock (_logLock)
        {
            var csvLine = $"{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                         $"\"{logEntry.EventType}\"," +
                         $"\"{logEntry.Title}\"," +
                         $"\"{logEntry.Url}\"," +
                         $"{logEntry.Confidence:F2}," +
                         $"\"{logEntry.WindowTitle}\"," +
                         $"\"{logEntry.ProcessName}\"," +
                         $"\"{logEntry.ScreenshotFileName}\"";
            
            // Write CSV header if file doesn't exist
            if (!File.Exists(csvFilePath))
            {
                var header = "Timestamp,EventType,Title,Url,Confidence,WindowTitle,ProcessName,ScreenshotFileName";
                File.WriteAllText(csvFilePath, header + Environment.NewLine);
            }
            
            // Append new entry
            File.AppendAllText(csvFilePath, csvLine + Environment.NewLine);
        }
    }

    /// <summary>
    /// Gets all media events for a specific date
    /// </summary>
    public async Task<List<MediaDetectionLogEntry>> GetEventsForDateAsync(DateTime date)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var logFilePath = Path.Combine(_logDirectory, $"media_events_{dateStr}.json");
        
        if (!File.Exists(logFilePath))
            return new List<MediaDetectionLogEntry>();
        
        try
        {
            var json = await File.ReadAllTextAsync(logFilePath);
            return JsonSerializer.Deserialize<List<MediaDetectionLogEntry>>(json) ?? new List<MediaDetectionLogEntry>();
        }
        catch (Exception ex)
        {
            _logger.Error("EventLogging", $"Error reading events for {dateStr}: {ex.Message}");
            return new List<MediaDetectionLogEntry>();
        }
    }

    /// <summary>
    /// Gets recent media events (last 24 hours)
    /// </summary>
    public async Task<List<MediaDetectionLogEntry>> GetRecentEventsAsync()
    {
        var events = new List<MediaDetectionLogEntry>();
        var yesterday = DateTime.Now.AddDays(-1);
        
        // Get events from yesterday and today
        events.AddRange(await GetEventsForDateAsync(yesterday));
        events.AddRange(await GetEventsForDateAsync(DateTime.Now));
        
        // Filter to last 24 hours
        var cutoffTime = DateTime.Now.AddHours(-24);
        return events.Where(e => e.Timestamp >= cutoffTime).ToList();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Log entry for media detection events
/// </summary>
public class MediaDetectionLogEntry
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string WindowTitle { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string? ScreenshotFileName { get; set; }
}
