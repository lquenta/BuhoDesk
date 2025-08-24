using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BuhoShared.Services;

/// <summary>
/// Service for discovering BuhoDesk servers on the network
/// </summary>
public class NetworkDiscoveryService : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly UdpClient _serverUdpClient; // Separate client for server listening
    private readonly UdpClient _clientUdpClient; // Separate client for client listening
    private readonly ILogger _logger;
    private readonly List<DiscoveredServer> _discoveredServers;
    private readonly Timer _discoveryTimer;
    private bool _isDiscovering = false;
    private const int DISCOVERY_PORT = 8082; // Different from main app ports
    private const int DISCOVERY_TIMEOUT = 3000; // 3 seconds timeout

    public event EventHandler<List<DiscoveredServer>>? ServersDiscovered;

    public NetworkDiscoveryService(ILogger logger)
    {
        _logger = logger;
        _discoveredServers = new List<DiscoveredServer>();
        
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        
        _serverUdpClient = new UdpClient();
        _serverUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        
        _clientUdpClient = new UdpClient();
        _clientUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        
        _discoveryTimer = new Timer(OnDiscoveryTimeout, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts discovering servers on the network
    /// </summary>
    public async Task<List<DiscoveredServer>> DiscoverServersAsync()
    {
        if (_isDiscovering)
        {
            _logger.Warning("NetworkDiscovery", "Discovery already in progress");
            return _discoveredServers.ToList();
        }

        _isDiscovering = true;
        _discoveredServers.Clear();

        try
        {
            _logger.Info("NetworkDiscovery", "Starting server discovery...");

            // Start listening for responses
            _ = Task.Run(ListenForResponsesAsync);

            // Send discovery broadcast
            await SendDiscoveryBroadcastAsync();

            // Wait for responses with timeout
            _discoveryTimer.Change(DISCOVERY_TIMEOUT, Timeout.Infinite);

            return _discoveredServers.ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkDiscovery", $"Failed to discover servers: {ex.Message}");
            _isDiscovering = false;
            return new List<DiscoveredServer>();
        }
    }

    /// <summary>
    /// Sends a discovery broadcast to find servers
    /// </summary>
    private async Task SendDiscoveryBroadcastAsync()
    {
        try
        {
            var discoveryMessage = new DiscoveryMessage
            {
                Type = "DISCOVERY_REQUEST",
                ClientId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(discoveryMessage);
            var data = Encoding.UTF8.GetBytes(json);

            // Send to broadcast address
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
            await _udpClient.SendAsync(data, data.Length, broadcastEndPoint);

            _logger.Info("NetworkDiscovery", "Discovery broadcast sent");
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkDiscovery", $"Failed to send discovery broadcast: {ex.Message}");
        }
    }

    /// <summary>
    /// Listens for server responses
    /// </summary>
    private async Task ListenForResponsesAsync()
    {
        try
        {
            // Bind client UDP client to listen for responses
            var clientEndPoint = new IPEndPoint(IPAddress.Any, 0); // Let OS choose available port
            _udpClient.Client.Bind(clientEndPoint);
            _udpClient.Client.ReceiveTimeout = DISCOVERY_TIMEOUT;
            
            var localEndPoint = _udpClient.Client.LocalEndPoint as IPEndPoint;
            _logger.Info("NetworkDiscovery", $"Client listening for responses on port {localEndPoint?.Port ?? 0}");
            
            while (_isDiscovering)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    
                    var response = JsonSerializer.Deserialize<DiscoveryResponse>(json);
                    if (response?.Type == "DISCOVERY_RESPONSE")
                    {
                        var server = new DiscoveredServer
                        {
                            IpAddress = result.RemoteEndPoint.Address.ToString(),
                            Port = response.Port,
                            ServerId = response.ServerId,
                            ServerName = response.ServerName,
                            DiscoveredAt = DateTime.Now
                        };

                        // Avoid duplicates
                        if (!_discoveredServers.Any(s => s.IpAddress == server.IpAddress && s.Port == server.Port))
                        {
                            _discoveredServers.Add(server);
                            _logger.Info("NetworkDiscovery", $"Discovered server: {server.ServerName} at {server.IpAddress}:{server.Port}");
                            
                            // Notify UI
                            ServersDiscovered?.Invoke(this, _discoveredServers.ToList());
                        }
                    }
                }
                catch (SocketException) when (!_isDiscovering)
                {
                    // Expected when stopping discovery
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning("NetworkDiscovery", $"Error processing discovery response: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkDiscovery", $"Error in discovery listener: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles discovery timeout
    /// </summary>
    private void OnDiscoveryTimeout(object? state)
    {
        _isDiscovering = false;
        _logger.Info("NetworkDiscovery", $"Discovery completed. Found {_discoveredServers.Count} servers");
    }

    /// <summary>
    /// Starts listening for discovery requests (for servers)
    /// </summary>
    public async Task StartListeningForRequestsAsync(int serverPort, string serverId, string serverName)
    {
        try
        {
            var localEndPoint = new IPEndPoint(IPAddress.Any, DISCOVERY_PORT);
            _serverUdpClient.Client.Bind(localEndPoint);

            _logger.Info("NetworkDiscovery", "Started listening for discovery requests");

            while (true)
            {
                try
                {
                    var result = await _serverUdpClient.ReceiveAsync();
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    
                    var request = JsonSerializer.Deserialize<DiscoveryMessage>(json);
                    if (request?.Type == "DISCOVERY_REQUEST")
                    {
                        // Send response back to the client
                        var response = new DiscoveryResponse
                        {
                            Type = "DISCOVERY_RESPONSE",
                            ServerId = serverId,
                            ServerName = serverName,
                            Port = serverPort,
                            Timestamp = DateTime.UtcNow
                        };

                        var responseJson = JsonSerializer.Serialize(response);
                        var responseData = Encoding.UTF8.GetBytes(responseJson);
                        
                        await _serverUdpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                        
                        _logger.Info("NetworkDiscovery", $"Responded to discovery request from {result.RemoteEndPoint}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Expected when server is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning("NetworkDiscovery", $"Error processing discovery request: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("NetworkDiscovery", $"Error in discovery listener: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops discovery
    /// </summary>
    public void StopDiscovery()
    {
        _isDiscovering = false;
        _discoveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.Info("NetworkDiscovery", "Discovery stopped");
    }

    /// <summary>
    /// Stops the discovery service (for servers)
    /// </summary>
    public void StopDiscoveryService()
    {
        try
        {
            _serverUdpClient?.Close();
            _logger.Info("NetworkDiscovery", "Discovery service stopped");
        }
        catch (Exception ex)
        {
            _logger.Warning("NetworkDiscovery", $"Error stopping discovery service: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopDiscovery();
        _discoveryTimer?.Dispose();
        
        try
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
        catch { }
        
        try
        {
            _serverUdpClient?.Close();
            _serverUdpClient?.Dispose();
        }
        catch { }
        
        try
        {
            _clientUdpClient?.Close();
            _clientUdpClient?.Dispose();
        }
        catch { }
    }
}

/// <summary>
/// Represents a discovered server
/// </summary>
public class DiscoveredServer
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public DateTime DiscoveredAt { get; set; }

    public override string ToString()
    {
        return $"{ServerName} ({IpAddress}:{Port})";
    }
}

/// <summary>
/// Discovery request message
/// </summary>
public class DiscoveryMessage
{
    public string Type { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Discovery response message
/// </summary>
public class DiscoveryResponse
{
    public string Type { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime Timestamp { get; set; }
}
