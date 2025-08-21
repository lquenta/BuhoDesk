using System.Drawing;

namespace BuhoShared.Models;

public class ScreenFrame
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime Timestamp { get; set; }
    public int FrameNumber { get; set; }
    public bool IsKeyFrame { get; set; }
}

public class MouseEvent
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButton Button { get; set; }
    public MouseEventType EventType { get; set; }
    public int ScrollDelta { get; set; }
}

public class KeyboardEvent
{
    public int KeyCode { get; set; }
    public bool IsKeyDown { get; set; }
    public bool IsCtrlPressed { get; set; }
    public bool IsAltPressed { get; set; }
    public bool IsShiftPressed { get; set; }
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public enum MouseEventType
{
    Move,
    Down,
    Up,
    Scroll
}

public class ConnectionInfo
{
    public string ServerId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime ConnectedAt { get; set; }
}

// UDP-related classes for screen sharing
public class UdpFrameHeader
{
    public string FrameId { get; set; } = string.Empty;
    public int FrameNumber { get; set; }
    public int TotalChunks { get; set; }
    public int TotalSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsKeyFrame { get; set; }
    public DateTime Timestamp { get; set; }
}

public class UdpClientRegistration
{
    public string ClientId { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
}

public class UdpServerResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UdpFrameChunk
{
    public string FrameId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
