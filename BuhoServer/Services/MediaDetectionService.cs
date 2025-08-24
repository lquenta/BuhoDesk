using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BuhoShared.Models;
using BuhoShared.Services;

namespace BuhoServer.Services;

/// <summary>
/// Service for detecting media content like YouTube, Netflix, etc.
/// </summary>
public class MediaDetectionService : IDisposable
{
    private readonly ILogger _logger;
    private readonly Timer _detectionTimer;
    private readonly List<MediaDetectionEvent> _recentEvents = new();
    private readonly object _eventsLock = new();
    private bool _isRunning = false;
    private DateTime _lastDetectionTime = DateTime.MinValue;
    private readonly ScreenCaptureService _screenCaptureService;
    
    // Media detection patterns
    private static readonly Dictionary<string, (string Pattern, string Type)> MediaPatterns = new()
    {
        { "YouTube", (@"youtube\.com/watch|youtube\.com/embed|youtu\.be/", "YouTube") },
        { "Netflix", (@"netflix\.com/watch", "Netflix") },
        { "Twitch", (@"twitch\.tv/", "Twitch") },
        { "Disney+", (@"disneyplus\.com/", "Disney+") },
        { "Prime Video", (@"amazon\.com/.*/dp/|amazon\.com/gp/video/", "Prime Video") },
        { "Hulu", (@"hulu\.com/watch", "Hulu") },
        { "Vimeo", (@"vimeo\.com/", "Vimeo") },
        { "Dailymotion", (@"dailymotion\.com/", "Dailymotion") }
    };

    public event EventHandler<MediaDetectionEvent>? MediaDetected;

    public MediaDetectionService(ILogger logger, ScreenCaptureService screenCaptureService)
    {
        _logger = logger;
        _screenCaptureService = screenCaptureService;
        _detectionTimer = new Timer(OnDetectionTimer, null, Timeout.Infinite, Timeout.Infinite);
        
        _logger.Info("MediaDetection", "MediaDetectionService initialized");
    }

    public void StartDetection()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _detectionTimer.Change(0, 5000); // Check every 5 seconds
        
        _logger.Info("MediaDetection", "Media detection started");
    }

    public void StopDetection()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _detectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        _logger.Info("MediaDetection", "Media detection stopped");
    }

    private async void OnDetectionTimer(object? state)
    {
        if (!_isRunning) return;

        try
        {
            await DetectMediaContentAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("MediaDetection", $"Error during media detection: {ex.Message}");
        }
    }

    private async Task DetectMediaContentAsync()
    {
        var activeWindows = GetActiveWindows();
        
        foreach (var window in activeWindows)
        {
            try
            {
                var mediaEvent = await AnalyzeWindowForMediaAsync(window);
                if (mediaEvent != null)
                {
                    // Check if this is a new event (not detected recently)
                    if (!IsRecentEvent(mediaEvent))
                    {
                        _logger.Info("MediaDetection", $"Media detected: {mediaEvent}");
                        
                        lock (_eventsLock)
                        {
                            _recentEvents.Add(mediaEvent);
                            // Keep only last 20 events
                            if (_recentEvents.Count > 20)
                                _recentEvents.RemoveAt(0);
                        }
                        
                        MediaDetected?.Invoke(this, mediaEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning("MediaDetection", $"Error analyzing window '{window.Title}': {ex.Message}");
            }
        }
    }

    private async Task<MediaDetectionEvent?> AnalyzeWindowForMediaAsync(WindowInfo window)
    {
        // Skip system windows
        if (string.IsNullOrEmpty(window.Title) || 
            window.Title.StartsWith("BuhoDesk") ||
            window.Title.Contains("Task Manager") ||
            window.Title.Contains("Settings"))
        {
            return null;
        }

        // Check if it's a browser window
        if (IsBrowserWindow(window.ProcessName))
        {
            var url = await ExtractUrlFromBrowserAsync(window);
            if (!string.IsNullOrEmpty(url))
            {
                return await CheckUrlForMediaAsync(url, window);
            }
        }

        // Check window title for media indicators
        return await CheckTitleForMediaAsync(window);
    }

    private async Task<MediaDetectionEvent?> CheckUrlForMediaAsync(string url, WindowInfo window)
    {
        foreach (var (name, (pattern, type)) in MediaPatterns)
        {
            if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
            {
                var screenshot = await CaptureScreenshotAsync();
                return new MediaDetectionEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = type,
                    Url = url,
                    Title = ExtractTitleFromUrl(url),
                    Confidence = 0.95, // High confidence for URL match
                    WindowTitle = window.Title,
                    ProcessName = window.ProcessName,
                    Screenshot = screenshot
                };
            }
        }

        return null;
    }

    private async Task<MediaDetectionEvent?> CheckTitleForMediaAsync(WindowInfo window)
    {
        var title = window.Title.ToLower();
        
        // Check for video-related keywords
        var videoKeywords = new[] { "youtube", "netflix", "twitch", "disney+", "prime video", "hulu", "vimeo" };
        
        foreach (var keyword in videoKeywords)
        {
            if (title.Contains(keyword))
            {
                var screenshot = await CaptureScreenshotAsync();
                return new MediaDetectionEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = keyword,
                    Url = "",
                    Title = window.Title,
                    Confidence = 0.7, // Medium confidence for title match
                    WindowTitle = window.Title,
                    ProcessName = window.ProcessName,
                    Screenshot = screenshot
                };
            }
        }

        return null;
    }

    private bool IsBrowserWindow(string processName)
    {
        var browsers = new[] { "chrome", "firefox", "edge", "opera", "brave", "safari" };
        return browsers.Any(browser => processName.ToLower().Contains(browser));
    }

    private async Task<string> ExtractUrlFromBrowserAsync(WindowInfo window)
    {
        // This is a simplified implementation
        // In a real implementation, you might use browser automation or APIs
        try
        {
            // For now, we'll try to extract from window title
            // Most browsers show the page title, not the URL
            return "";
        }
        catch (Exception ex)
        {
            _logger.Debug("MediaDetection", $"Could not extract URL from {window.ProcessName}: {ex.Message}");
            return "";
        }
    }

    private string ExtractTitleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    private async Task<byte[]> CaptureScreenshotAsync()
    {
        try
        {
            // Use the existing screen capture service to capture the screen
            var screenshot = await _screenCaptureService.CaptureScreenAsync();
            if (screenshot != null && screenshot.ImageData != null)
            {
                return screenshot.ImageData;
            }
            
            _logger.Warning("MediaDetection", "Failed to capture screenshot - no image data");
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            _logger.Error("MediaDetection", $"Error capturing screenshot: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private bool IsRecentEvent(MediaDetectionEvent mediaEvent)
    {
        lock (_eventsLock)
        {
            return _recentEvents.Any(e => 
                e.EventType == mediaEvent.EventType && 
                e.Url == mediaEvent.Url &&
                (DateTime.Now - e.Timestamp).TotalMinutes < 1);
        }
    }

    private List<WindowInfo> GetActiveWindows()
    {
        var windows = new List<WindowInfo>();
        
        try
        {
            var processes = Process.GetProcesses();
            
            foreach (var process in processes)
            {
                try
                {
                    if (!string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        windows.Add(new WindowInfo
                        {
                            Title = process.MainWindowTitle,
                            ProcessName = process.ProcessName,
                            ProcessId = process.Id
                        });
                    }
                }
                catch
                {
                    // Skip processes we can't access
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("MediaDetection", $"Error getting active windows: {ex.Message}");
        }

        return windows;
    }

    public List<MediaDetectionEvent> GetRecentEvents()
    {
        lock (_eventsLock)
        {
            return _recentEvents.ToList();
        }
    }

    public void Dispose()
    {
        StopDetection();
        _detectionTimer?.Dispose();
    }
}

public class WindowInfo
{
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}
