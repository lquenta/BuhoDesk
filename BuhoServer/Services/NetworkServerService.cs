using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BuhoShared.Network;
using BuhoShared.Models;
using BuhoShared.Services;

namespace BuhoServer.Services;

public class NetworkServerService : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Dictionary<string, TcpClient> _clients = new();
    private readonly object _clientsLock = new();
    private readonly InputSimulationService _inputService;
    private readonly ScreenCaptureService _screenService;
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _isRunning = false;
    private readonly string _serverId;
    private int _totalFramesSent = 0;
    private int _totalBytesSent = 0;
    private DateTime _startTime;

    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<ChatMessage>? ChatMessageReceived;

    public NetworkServerService(int port, InputSimulationService inputService, ScreenCaptureService screenService, ILogger logger)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _inputService = inputService;
        _screenService = screenService;
        _logger = logger;
        _performanceMonitor = new PerformanceMonitor(logger);
        _serverId = Guid.NewGuid().ToString("N")[..8].ToUpper();
        
        _screenService.FrameCaptured += OnFrameCaptured;
        
        _logger.Info("NetworkServer", $"NetworkServerService initialized on port {port}, Server ID: {_serverId}");
    }

    public string ServerId => _serverId;

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _startTime = DateTime.UtcNow;
        _totalFramesSent = 0;
        _totalBytesSent = 0;
        
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        
        _logger.Info("NetworkServer", $"Server started on port {port}");
        _logger.Info("NetworkServer", $"Server ID: {_serverId}");
        _logger.Info("NetworkServer", "Waiting for client connections...");

        Task.Run(AcceptClientsAsync);
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _listener.Stop();

        var uptime = DateTime.UtcNow - _startTime;
        _logger.Info("NetworkServer", $"Server stopping after {uptime.TotalMinutes:F1} minutes uptime");
        _logger.Info("NetworkServer", $"Total frames sent: {_totalFramesSent}, Total bytes: {_totalBytesSent:N0}");

        lock (_clientsLock)
        {
            _logger.Info("NetworkServer", $"Disconnecting {_clients.Count} active clients");
            foreach (var client in _clients.Values)
            {
                client.Close();
            }
            _clients.Clear();
        }
        
        _logger.Info("NetworkServer", "Server stopped");
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                var clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                _logger.Info("NetworkServer", $"New client connection from {clientEndPoint}");
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("NetworkServer", $"Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var clientId = string.Empty;
        var stream = client.GetStream();
        var buffer = new byte[4096];

        try
        {
            while (_isRunning && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                _logger.Debug("NetworkServer", $"Received raw message: {message}");
                
                try
                {
                    var networkMessage = JsonSerializer.Deserialize<NetworkMessage>(message);
                    if (networkMessage != null)
                    {
                        _logger.Debug("NetworkServer", $"Deserialized message type: {networkMessage.Type}");
                        clientId = await ProcessMessageAsync(client, networkMessage, clientId);
                    }
                    else
                    {
                        _logger.Error("NetworkServer", "Failed to deserialize message");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("NetworkServer", $"JSON deserialization error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkServer", $"Client handling error: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(clientId))
            {
                lock (_clientsLock)
                {
                    _clients.Remove(clientId);
                }
                _logger.Info("NetworkServer", $"Client {clientId} disconnected");
                ClientDisconnected?.Invoke(this, clientId);
            }
            client.Close();
        }
    }

    private async Task<string> ProcessMessageAsync(TcpClient client, NetworkMessage message, string clientId)
    {
        switch (message.Type)
        {
            case MessageType.Connect:
                var connectRequest = message.GetData<ConnectionRequest>();
                if (connectRequest != null)
                {
                    clientId = connectRequest.ClientId;
                    lock (_clientsLock)
                    {
                        _clients[clientId] = client;
                    }

                    var response = NetworkMessage.Create(MessageType.Connect, new ConnectionResponse
                    {
                        Success = true,
                        Message = "Connected successfully",
                        ServerId = _serverId
                    });

                    await SendMessageAsync(client, response);
                    _logger.Info("NetworkServer", $"Client {clientId} connected successfully");
                    ClientConnected?.Invoke(this, clientId);
                    return clientId;
                }
                break;

            case MessageType.MouseEvent:
                var mouseEvent = message.GetData<MouseEvent>();
                if (mouseEvent != null)
                {
                    _inputService.SimulateMouseEvent(mouseEvent);
                }
                break;

            case MessageType.KeyboardEvent:
                var keyboardEvent = message.GetData<KeyboardEvent>();
                if (keyboardEvent != null)
                {
                    _inputService.SimulateKeyboardEvent(keyboardEvent);
                }
                break;

            case MessageType.Chat:
                var chatMessage = message.GetData<ChatMessage>();
                if (chatMessage != null)
                {
                    _logger.Info("NetworkServer", $"Chat message from {chatMessage.SenderName}: {chatMessage.Message}");
                    ChatMessageReceived?.Invoke(this, chatMessage);
                    
                    // Broadcast chat message to all connected clients
                    await BroadcastMessage(message);
                }
                break;

            case MessageType.Disconnect:
                client.Close();
                break;
        }
        
        return clientId;
    }

    private async void OnFrameCaptured(object? sender, ScreenFrame frame)
    {
        // Disabled TCP broadcast for screen frames - using UDP instead for better performance
        // This method is kept for compatibility but screen frames are now sent via UDP
        _logger.Debug("NetworkServer", $"Screen frame {frame.FrameNumber} captured - sending via UDP instead of TCP");
    }

    private async Task BroadcastMessage(NetworkMessage message)
    {
        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);

        List<string> disconnectedClients;
        
        lock (_clientsLock)
        {
            disconnectedClients = new List<string>();
        }

        var clientsToSend = new List<KeyValuePair<string, TcpClient>>();
        
        lock (_clientsLock)
        {
            clientsToSend.AddRange(_clients);
        }

        foreach (var kvp in clientsToSend)
        {
            try
            {
                if (kvp.Value.Connected)
                {
                    var stream = kvp.Value.GetStream();
                    await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                }
                else
                {
                    disconnectedClients.Add(kvp.Key);
                }
            }
            catch
            {
                disconnectedClients.Add(kvp.Key);
            }
        }

        if (disconnectedClients.Count > 0)
        {
            lock (_clientsLock)
            {
                foreach (var clientId in disconnectedClients)
                {
                    _clients.Remove(clientId);
                    ClientDisconnected?.Invoke(this, clientId);
                }
            }
        }
    }

    private async Task SendMessageAsync(TcpClient client, NetworkMessage message)
    {
        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var stream = client.GetStream();
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    public async Task SendChatMessageAsync(string message, string senderName = "Server")
    {
        var chatMessage = new ChatMessage
        {
            SenderId = _serverId,
            SenderName = senderName,
            Message = message,
            Timestamp = DateTime.UtcNow,
            IsFromServer = true
        };

        var networkMessage = NetworkMessage.Create(MessageType.Chat, chatMessage);
        
        // Trigger the event for the server's own message so it appears in the UI
        ChatMessageReceived?.Invoke(this, chatMessage);
        
        await BroadcastMessage(networkMessage);
        _logger.Info("NetworkServer", $"Server sent chat message: {message}");
    }

    public void Dispose()
    {
        Stop();
        _listener?.Dispose();
    }
}
