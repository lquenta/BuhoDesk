using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BuhoShared.Models;
using BuhoShared.Services;

namespace BuhoServer.Services;

public class UdpScreenService : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly Dictionary<string, IPEndPoint> _clients = new();
    private readonly object _clientsLock = new();
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _isRunning = false;
    private readonly string _serverId;
    private int _totalFramesSent = 0;
    private int _totalBytesSent = 0;
    private DateTime _startTime;
    
    // UDP optimization settings
    private const int UDP_PORT = 8081; // Different port for UDP
    private const int MAX_UDP_PACKET_SIZE = 65507; // Max UDP packet size
    private const int CHUNK_SIZE = 32000; // Reduced chunk size to fit in UDP packets with JSON overhead
    private const int FRAME_TIMEOUT_MS = 200; // Increased timeout for frame chunks

    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;

    public UdpScreenService(ILogger logger)
    {
        _udpClient = new UdpClient(UDP_PORT);
        _logger = logger;
        _performanceMonitor = new PerformanceMonitor(logger);
        _serverId = Guid.NewGuid().ToString("N")[..8].ToUpper();
        
        _logger.Info("UdpScreen", $"UdpScreenService initialized on port {UDP_PORT}, Server ID: {_serverId}");
    }

    public string ServerId => _serverId;

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _startTime = DateTime.UtcNow;
        _totalFramesSent = 0;
        _totalBytesSent = 0;
        
        _logger.Info("UdpScreen", $"UDP screen service started on port {UDP_PORT}");
        _logger.Info("UdpScreen", $"Server ID: {_serverId}");

        Task.Run(ReceiveClientRegistrationsAsync);
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _udpClient.Close();

        var uptime = DateTime.UtcNow - _startTime;
        _logger.Info("UdpScreen", $"UDP screen service stopping after {uptime.TotalMinutes:F1} minutes uptime");
        _logger.Info("UdpScreen", $"Total frames sent: {_totalFramesSent}, Total bytes: {_totalBytesSent:N0}");
        
        _logger.Info("UdpScreen", "UDP screen service stopped");
    }

    private async Task ReceiveClientRegistrationsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                var message = Encoding.UTF8.GetString(result.Buffer);
                
                try
                {
                    var registration = JsonSerializer.Deserialize<UdpClientRegistration>(message);
                    if (registration != null)
                    {
                        lock (_clientsLock)
                        {
                            _clients[registration.ClientId] = result.RemoteEndPoint;
                        }
                        
                        _logger.Info("UdpScreen", $"Client {registration.ClientId} registered for UDP frames from {result.RemoteEndPoint}");
                        ClientConnected?.Invoke(this, registration.ClientId);
                        
                        // Send confirmation
                        var response = new UdpServerResponse { Success = true, Message = "UDP registration successful" };
                        var responseJson = JsonSerializer.Serialize(response);
                        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                        await _udpClient.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("UdpScreen", $"Failed to process client registration: {ex.Message}");
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("UdpScreen", $"Error receiving client registration: {ex.Message}");
            }
        }
    }

    public async Task BroadcastFrameAsync(ScreenFrame frame)
    {
        if (!_isRunning) return;

        using var timer = _performanceMonitor.TimeOperation("BroadcastUdpFrame", "UdpScreen");
        
        var frameData = frame.ImageData;
        var frameId = Guid.NewGuid().ToString("N")[..8];
        var totalChunks = (frameData.Length + CHUNK_SIZE - 1) / CHUNK_SIZE;
        
        _logger.Debug("UdpScreen", $"Broadcasting frame {frame.FrameNumber} ({frameData.Length} bytes) in {totalChunks} chunks to {_clients.Count} clients");

        var clientsToSend = new List<KeyValuePair<string, IPEndPoint>>();
        
        lock (_clientsLock)
        {
            clientsToSend.AddRange(_clients);
        }

        var tasks = new List<Task>();

        foreach (var kvp in clientsToSend)
        {
            tasks.Add(SendFrameToClientAsync(kvp.Value, frame, frameId, totalChunks));
        }

        await Task.WhenAll(tasks);
        
        Interlocked.Increment(ref _totalFramesSent);
        Interlocked.Add(ref _totalBytesSent, frameData.Length);
    }

    private async Task SendFrameToClientAsync(IPEndPoint clientEndPoint, ScreenFrame frame, string frameId, int totalChunks)
    {
        try
        {
            var frameData = frame.ImageData;
            
            // Send frame header
            var header = new UdpFrameHeader
            {
                FrameId = frameId,
                FrameNumber = frame.FrameNumber,
                TotalChunks = totalChunks,
                TotalSize = frameData.Length,
                Width = frame.Width,
                Height = frame.Height,
                IsKeyFrame = frame.IsKeyFrame,
                Timestamp = frame.Timestamp
            };

            var headerJson = JsonSerializer.Serialize(header);
            var headerBytes = Encoding.UTF8.GetBytes(headerJson);
            
            // Send header
            await _udpClient.SendAsync(headerBytes, headerBytes.Length, clientEndPoint);
            
            // Send chunks
            for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                var offset = chunkIndex * CHUNK_SIZE;
                var chunkSize = Math.Min(CHUNK_SIZE, frameData.Length - offset);
                var chunk = new byte[chunkSize];
                Array.Copy(frameData, offset, chunk, 0, chunkSize);
                
                // Create binary chunk packet (more efficient than JSON)
                var frameIdBytes = Encoding.UTF8.GetBytes(frameId);
                var headerSize = 4 + 4 + 4 + frameIdBytes.Length; // FrameIdLength + ChunkIndex + TotalChunks + FrameId
                var packetBytes = new byte[headerSize + chunk.Length];
                
                var packetOffset = 0;
                // FrameId length (4 bytes)
                BitConverter.GetBytes(frameIdBytes.Length).CopyTo(packetBytes, packetOffset);
                packetOffset += 4;
                // ChunkIndex (4 bytes)
                BitConverter.GetBytes(chunkIndex).CopyTo(packetBytes, packetOffset);
                packetOffset += 4;
                // TotalChunks (4 bytes)
                BitConverter.GetBytes(totalChunks).CopyTo(packetBytes, packetOffset);
                packetOffset += 4;
                // FrameId (variable length)
                frameIdBytes.CopyTo(packetBytes, packetOffset);
                packetOffset += frameIdBytes.Length;
                // Chunk data
                chunk.CopyTo(packetBytes, packetOffset);
                
                await _udpClient.SendAsync(packetBytes, packetBytes.Length, clientEndPoint);
                
                // Reduced delay to prevent overwhelming the network
                if (chunkIndex % 10 == 0) // Only delay every 10th chunk (was every 5th)
                {
                    await Task.Delay(1);
                }
            }
            
            _logger.Debug("UdpScreen", $"Sent frame {frame.FrameNumber} ({frameData.Length} bytes) to {clientEndPoint}");
        }
        catch (Exception ex)
        {
            _logger.Error("UdpScreen", $"Failed to send frame to {clientEndPoint}: {ex.Message}");
        }
    }

    public void RemoveClient(string clientId)
    {
        lock (_clientsLock)
        {
            if (_clients.Remove(clientId))
            {
                _logger.Info("UdpScreen", $"Client {clientId} removed from UDP broadcast");
                ClientDisconnected?.Invoke(this, clientId);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
    }
}


