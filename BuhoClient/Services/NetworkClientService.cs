using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BuhoShared.Network;
using BuhoShared.Models;
using BuhoShared.Services;

namespace BuhoClient.Services;

public class NetworkClientService : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _clientId;
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _isConnected = false;
    private readonly object _lockObject = new();
    private int _framesReceived = 0;
    private int _bytesReceived = 0;

    public event EventHandler<ScreenFrame>? FrameReceived;
    public event EventHandler<ConnectionResponse>? ConnectionResponseReceived;
    public event EventHandler<string>? ConnectionStatusChanged;
    public event EventHandler<ChatMessage>? ChatMessageReceived;

    public NetworkClientService(ILogger logger)
    {
        _clientId = Guid.NewGuid().ToString("N")[..8].ToUpper();
        _logger = logger;
        _performanceMonitor = new PerformanceMonitor(logger);
        
        _logger.Info("NetworkClient", $"NetworkClientService initialized, Client ID: {_clientId}");
    }

    public string ClientId => _clientId;
    public bool IsConnected => _isConnected;

    public async Task<bool> ConnectAsync(string serverAddress, int port, string clientName = "")
    {
        try
        {
            _logger.Info("NetworkClient", $"Attempting to connect to {serverAddress}:{port}");
            
            _client = new TcpClient();
            await _client.ConnectAsync(serverAddress, port);
            _stream = _client.GetStream();

            _logger.Info("NetworkClient", "TCP connection established");

            var connectRequest = new ConnectionRequest
            {
                ClientId = _clientId,
                ServerId = "",
                ClientName = string.IsNullOrEmpty(clientName) ? $"Client-{_clientId}" : clientName
            };

            _logger.Info("NetworkClient", $"Sending connection request with client name: {connectRequest.ClientName}");

            var message = NetworkMessage.Create(MessageType.Connect, connectRequest);
            await SendMessageAsync(message);

            // Start receiving messages immediately to get the connection response
            _ = Task.Run(ReceiveMessagesAsync);
            
            // Don't set connected yet - wait for server response
            _logger.Info("NetworkClient", "Connection request sent, waiting for server response...");

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkClient", $"Connection failed to {serverAddress}:{port}", ex);
            ConnectionStatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        lock (_lockObject)
        {
            if (_isConnected)
            {
                _isConnected = false;
                
                try
                {
                    var disconnectMessage = NetworkMessage.Create(MessageType.Disconnect, new { });
                    _ = SendMessageAsync(disconnectMessage);
                }
                catch { }

                _stream?.Close();
                _client?.Close();
                
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
            }
        }
    }

    public async Task SendMouseEventAsync(MouseEvent mouseEvent)
    {
        if (!_isConnected) return;

        try
        {
            var message = NetworkMessage.Create(MessageType.MouseEvent, mouseEvent);
            await SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkClient", $"Error sending mouse event: {ex.Message}");
        }
    }

    public async Task SendKeyboardEventAsync(KeyboardEvent keyboardEvent)
    {
        if (!_isConnected) return;

        try
        {
            var message = NetworkMessage.Create(MessageType.KeyboardEvent, keyboardEvent);
            await SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkClient", $"Error sending keyboard event: {ex.Message}");
        }
    }

    public async Task SendChatMessageAsync(string message, string senderName)
    {
        if (!_isConnected) return;

        try
        {
            var chatMessage = new ChatMessage
            {
                SenderId = _clientId,
                SenderName = senderName,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsFromServer = false
            };

            var networkMessage = NetworkMessage.Create(MessageType.Chat, chatMessage);
            await SendMessageAsync(networkMessage);
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkClient", $"Error sending chat message: {ex.Message}");
        }
    }

    private async Task SendMessageAsync(NetworkMessage message)
    {
        if (_stream == null) return;

        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            await _stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkClient", $"Error sending message: {ex.Message}");
            throw;
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        while (_stream != null && _client?.Connected == true)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(chunk);
                
                // Try to extract complete JSON messages
                var messageText = messageBuffer.ToString();
                var braceCount = 0;
                var startIndex = 0;
                
                for (int i = 0; i < messageText.Length; i++)
                {
                    if (messageText[i] == '{')
                    {
                        if (braceCount == 0) startIndex = i;
                        braceCount++;
                    }
                    else if (messageText[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            // Complete JSON message found
                            var jsonMessage = messageText.Substring(startIndex, i - startIndex + 1);
                            _logger.Debug("NetworkClient", $"Extracted JSON message: {jsonMessage}");
                            
                            try
                            {
                                var networkMessage = JsonSerializer.Deserialize<NetworkMessage>(jsonMessage);
                                if (networkMessage != null)
                                {
                                    _logger.Debug("NetworkClient", $"Deserialized message type: {networkMessage.Type}");
                                    ProcessMessage(networkMessage);
                                }
                                else
                                {
                                    _logger.Error("NetworkClient", "Failed to deserialize message");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("NetworkClient", $"JSON deserialization error: {ex.Message}");
                            }
                            
                            // Remove processed message from buffer
                            messageBuffer.Remove(0, i + 1);
                            messageText = messageBuffer.ToString();
                            i = -1; // Reset loop
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("NetworkClient", $"Error receiving message: {ex.Message}");
                break;
            }
        }

        Disconnect();
    }

    private void ProcessMessage(NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.Connect:
                var response = message.GetData<ConnectionResponse>();
                if (response != null)
                {
                    if (response.Success)
                    {
                        _isConnected = true;
                        _framesReceived = 0;
                        _bytesReceived = 0;
                        
                        ConnectionStatusChanged?.Invoke(this, "Connected");
                        _logger.Info("NetworkClient", "Connection established successfully");
                    }
                    else
                    {
                        _logger.Error("NetworkClient", $"Connection failed: {response.Message}");
                        ConnectionStatusChanged?.Invoke(this, $"Connection failed: {response.Message}");
                        Disconnect();
                    }
                    ConnectionResponseReceived?.Invoke(this, response);
                }
                break;

            case MessageType.ScreenFrame:
                var frame = message.GetData<ScreenFrame>();
                if (frame != null)
                {
                    Interlocked.Increment(ref _framesReceived);
                    Interlocked.Add(ref _bytesReceived, frame.ImageData.Length);
                    
                    _logger.Debug("NetworkClient", $"Received frame {frame.FrameNumber} ({frame.ImageData.Length} bytes), Total frames: {_framesReceived}");
                    FrameReceived?.Invoke(this, frame);
                }
                break;

            case MessageType.Chat:
                var chatMessage = message.GetData<ChatMessage>();
                if (chatMessage != null)
                {
                    _logger.Info("NetworkClient", $"Chat message from {chatMessage.SenderName}: {chatMessage.Message}");
                    ChatMessageReceived?.Invoke(this, chatMessage);
                }
                break;

            case MessageType.Error:
                var error = message.GetData<object>();
                _logger.Error("NetworkClient", $"Server error: {error}");
                break;
        }
    }

    public void Dispose()
    {
        Disconnect();
        _stream?.Dispose();
        _client?.Dispose();
    }
}

