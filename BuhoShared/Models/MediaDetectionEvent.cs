using System;

namespace BuhoShared.Models;

/// <summary>
/// Represents a detected media event (YouTube, Netflix, etc.)
/// </summary>
public class MediaDetectionEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // "YouTube", "Netflix", etc.
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public byte[] Screenshot { get; set; } = Array.Empty<byte>();
    public double Confidence { get; set; }
    public string WindowTitle { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {EventType}: {Title} ({Url}) - Confidence: {Confidence:P0}";
    }
}
