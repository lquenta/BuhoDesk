using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BuhoShared.Models;
using BuhoShared.Services;

namespace BuhoClient.Services;

public class UdpScreenClientService : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _isRunning = false;
    private readonly string _clientId;
    private readonly Dictionary<string, UdpFrameReceiver> _frameReceivers = new();
    private readonly object _frameReceiversLock = new();
    private int _framesReceived = 0;
    private int _bytesReceived = 0;
    private DateTime _lastFpsReport = DateTime.MinValue;
    private int _framesSinceLastReport = 0;

    public event EventHandler<ScreenFrame>? FrameReceived;
    public event EventHandler<string>? ConnectionStatusChanged;

    public UdpScreenClientService(ILogger logger, string clientId)
    {
        _udpClient = new UdpClient(0); // Let OS choose port
        _logger = logger;
        _performanceMonitor = new PerformanceMonitor(logger);
        _clientId = clientId;
        
        _logger.Info("UdpScreenClient", $"UdpScreenClientService initialized, Client ID: {_clientId}");
    }

    public string ClientId => _clientId;
    public bool IsRunning => _isRunning;

    public async Task<bool> RegisterWithServerAsync(string serverAddress, int serverPort)
    {
        try
        {
            _logger.Info("UdpScreenClient", $"Registering with UDP server at {serverAddress}:{serverPort}");
            
            var registration = new UdpClientRegistration
            {
                ClientId = _clientId,
                ServerId = ""
            };

            var registrationJson = JsonSerializer.Serialize(registration);
            var registrationBytes = Encoding.UTF8.GetBytes(registrationJson);
            
            var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverAddress), serverPort);
            await _udpClient.SendAsync(registrationBytes, registrationBytes.Length, serverEndPoint);
            
            _logger.Info("UdpScreenClient", "UDP registration sent successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("UdpScreenClient", $"Failed to register with UDP server: {ex.Message}");
            return false;
        }
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _lastFpsReport = DateTime.UtcNow;
        _framesSinceLastReport = 0;
        
        _logger.Info("UdpScreenClient", "UDP screen client started");
        ConnectionStatusChanged?.Invoke(this, "UDP Connected");

        Task.Run(ReceiveFramesAsync);
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _udpClient.Close();
        
        lock (_frameReceiversLock)
        {
            _frameReceivers.Clear();
        }
        
        _logger.Info("UdpScreenClient", "UDP screen client stopped");
        ConnectionStatusChanged?.Invoke(this, "UDP Disconnected");
    }

    private async Task ReceiveFramesAsync()
    {
        while (_isRunning)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                var message = Encoding.UTF8.GetString(result.Buffer);
                
                try
                {
                    // Check if it's a binary chunk packet (starts with frame ID length)
                    if (result.Buffer.Length >= 12) // Minimum header size
                    {
                        var frameIdLength = BitConverter.ToInt32(result.Buffer, 0);
                        if (frameIdLength > 0 && frameIdLength < 100 && result.Buffer.Length >= 12 + frameIdLength)
                        {
                            // Parse binary chunk
                            var chunkIndex = BitConverter.ToInt32(result.Buffer, 4);
                            var totalChunks = BitConverter.ToInt32(result.Buffer, 8);
                            var frameId = Encoding.UTF8.GetString(result.Buffer, 12, frameIdLength);
                            var dataLength = result.Buffer.Length - 12 - frameIdLength;
                            var data = new byte[dataLength];
                            Array.Copy(result.Buffer, 12 + frameIdLength, data, 0, dataLength);
                            
                            var chunk = new UdpFrameChunk
                            {
                                FrameId = frameId,
                                ChunkIndex = chunkIndex,
                                TotalChunks = totalChunks,
                                Data = data
                            };
                            
                            HandleFrameChunkAsync(chunk, result.RemoteEndPoint);
                            continue;
                        }
                    }
                    
                    // Try to parse as JSON (frame header or server response)
                    // Try to parse as frame header first
                    var header = JsonSerializer.Deserialize<UdpFrameHeader>(message);
                    if (header != null)
                    {
                        HandleFrameHeaderAsync(header, result.RemoteEndPoint);
                        continue;
                    }
                    
                    // Try to parse as server response
                    var response = JsonSerializer.Deserialize<UdpServerResponse>(message);
                    if (response != null)
                    {
                        _logger.Info("UdpScreenClient", $"Server response: {response.Message}");
                        continue;
                    }
                    
                    _logger.Warning("UdpScreenClient", $"Unknown message type received: {message.Substring(0, Math.Min(100, message.Length))}...");
                }
                catch (Exception ex)
                {
                    _logger.Error("UdpScreenClient", $"Failed to process received message: {ex.Message}");
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("UdpScreenClient", $"Error receiving frames: {ex.Message}");
            }
        }
    }

    private void HandleFrameHeaderAsync(UdpFrameHeader header, IPEndPoint remoteEndPoint)
    {
        try
        {
            _logger.Debug("UdpScreenClient", $"Received frame header: {header.FrameId}, {header.TotalChunks} chunks, {header.TotalSize} bytes");
            
            lock (_frameReceiversLock)
            {
                _frameReceivers[header.FrameId] = new UdpFrameReceiver(header);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("UdpScreenClient", $"Failed to handle frame header: {ex.Message}");
        }
    }

    private void HandleFrameChunkAsync(UdpFrameChunk chunk, IPEndPoint remoteEndPoint)
    {
        try
        {
            lock (_frameReceiversLock)
            {
                if (_frameReceivers.TryGetValue(chunk.FrameId, out var receiver))
                {
                    receiver.AddChunk(chunk);
                    
                    if (receiver.IsComplete)
                    {
                        var frame = receiver.BuildFrame();
                        if (frame != null)
                        {
                            Interlocked.Increment(ref _framesReceived);
                            Interlocked.Add(ref _bytesReceived, frame.ImageData.Length);
                            
                            _framesSinceLastReport++;
                            
                            // Report FPS periodically
                            var now = DateTime.UtcNow;
                            if ((now - _lastFpsReport).TotalSeconds >= 5)
                            {
                                var actualFps = _framesSinceLastReport / (now - _lastFpsReport).TotalSeconds;
                                _logger.Info("UdpScreenClient", $"UDP FPS: {actualFps:F2}, Total frames: {_framesReceived}, Total bytes: {_bytesReceived:N0}");
                                
                                _lastFpsReport = now;
                                _framesSinceLastReport = 0;
                            }
                            
                            _logger.Debug("UdpScreenClient", $"Completed frame {frame.FrameNumber} ({frame.ImageData.Length} bytes)");
                            FrameReceived?.Invoke(this, frame);
                        }
                        
                        _frameReceivers.Remove(chunk.FrameId);
                    }
                }
                else
                {
                    _logger.Warning("UdpScreenClient", $"Received chunk for unknown frame: {chunk.FrameId}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("UdpScreenClient", $"Failed to handle frame chunk: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
    }
}

// Frame receiver for assembling UDP chunks
public class UdpFrameReceiver
{
    private readonly UdpFrameHeader _header;
    private readonly Dictionary<int, byte[]> _chunks = new();
    private readonly object _chunksLock = new();

    public UdpFrameReceiver(UdpFrameHeader header)
    {
        _header = header;
    }

    public bool IsComplete
    {
        get
        {
            lock (_chunksLock)
            {
                return _chunks.Count == _header.TotalChunks;
            }
        }
    }

    public void AddChunk(UdpFrameChunk chunk)
    {
        lock (_chunksLock)
        {
            if (!_chunks.ContainsKey(chunk.ChunkIndex))
            {
                _chunks[chunk.ChunkIndex] = chunk.Data;
            }
        }
    }

    public ScreenFrame? BuildFrame()
    {
        lock (_chunksLock)
        {
            if (!IsComplete) return null;

            try
            {
                // Reconstruct the complete frame data
                var frameData = new byte[_header.TotalSize];
                var offset = 0;

                for (int i = 0; i < _header.TotalChunks; i++)
                {
                    if (_chunks.TryGetValue(i, out var chunk))
                    {
                        Array.Copy(chunk, 0, frameData, offset, chunk.Length);
                        offset += chunk.Length;
                    }
                }

                return new ScreenFrame
                {
                    ImageData = frameData,
                    Width = _header.Width,
                    Height = _header.Height,
                    Timestamp = _header.Timestamp,
                    FrameNumber = _header.FrameNumber,
                    IsKeyFrame = _header.IsKeyFrame
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}


