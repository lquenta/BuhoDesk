using System.Text.Json;
using BuhoShared.Models;

namespace BuhoShared.Network;

public class NetworkMessage
{
    public MessageType Type { get; set; }
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static NetworkMessage Create<T>(MessageType type, T data)
    {
        return new NetworkMessage
        {
            Type = type,
            Data = JsonSerializer.Serialize(data),
            Timestamp = DateTime.UtcNow
        };
    }

    public T? GetData<T>()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(Data);
        }
        catch
        {
            return default;
        }
    }
}

public enum MessageType
{
    Connect,
    Disconnect,
    ScreenFrame,
    MouseEvent,
    KeyboardEvent,
    ConnectionInfo,
    Heartbeat,
    Error,
    Chat
}

public class ConnectionRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
}

public class ConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
}
