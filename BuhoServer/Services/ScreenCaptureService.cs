using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using BuhoShared.Models;
using BuhoShared.Services;

namespace BuhoServer.Services;

public class ScreenCaptureService : IDisposable
{
    private readonly Timer _captureTimer;
    private readonly object _lockObject = new();
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _isCapturing = false;
    private int _frameNumber = 0;
    private DateTime _lastKeyFrame = DateTime.MinValue;
    private DateTime _lastFpsReport = DateTime.MinValue;
    private int _framesSinceLastReport = 0;
    
    // Performance optimization settings
    private const int KEY_FRAME_INTERVAL_MS = 5000; // Key frame every 5 seconds (increased)
    private const int CAPTURE_INTERVAL_MS = 100; // 10 FPS (reduced significantly)
    private const int FPS_REPORT_INTERVAL_MS = 10000; // Report every 10 seconds
    private const int MAX_QUALITY = 60; // JPEG quality (reduced further)
    private const int MIN_QUALITY = 40; // Minimum quality for fast transmission
    
    // Frame differencing - less aggressive
    private byte[]? _lastFrameData;
    private int _consecutiveSimilarFrames = 0;
    private const int SIMILAR_FRAME_THRESHOLD = 3; // Skip frames if 3 consecutive are similar
    private const double SIMILARITY_THRESHOLD = 0.95; // 95% similarity threshold (less aggressive)
    
    // Adaptive quality based on network performance
    private int _currentQuality = MAX_QUALITY;
    private DateTime _lastQualityAdjustment = DateTime.UtcNow;
    private const int QUALITY_ADJUSTMENT_INTERVAL_MS = 1000;

    public event EventHandler<ScreenFrame>? FrameCaptured;

    public ScreenCaptureService(ILogger logger)
    {
        _logger = logger;
        _performanceMonitor = new PerformanceMonitor(logger);
        _captureTimer = new Timer(CaptureScreen, null, Timeout.Infinite, Timeout.Infinite);
        
        _logger.Info("ScreenCapture", "ScreenCaptureService initialized with optimizations");
    }

    public void StartCapture()
    {
        lock (_lockObject)
        {
            if (!_isCapturing)
            {
                _isCapturing = true;
                _lastFpsReport = DateTime.UtcNow;
                _framesSinceLastReport = 0;
                _lastFrameData = null;
                _consecutiveSimilarFrames = 0;
                _currentQuality = MAX_QUALITY;
                _captureTimer.Change(0, CAPTURE_INTERVAL_MS);
                
                _logger.Info("ScreenCapture", $"Screen capture started (interval: {CAPTURE_INTERVAL_MS}ms, target FPS: {1000.0 / CAPTURE_INTERVAL_MS:F1})");
            }
        }
    }

    public void StopCapture()
    {
        lock (_lockObject)
        {
            if (_isCapturing)
            {
                _isCapturing = false;
                _captureTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                _logger.Info("ScreenCapture", "Screen capture stopped");
                
                // Log final performance metrics
                var metrics = _performanceMonitor.GetAllMetrics();
                foreach (var metric in metrics.Values)
                {
                    _logger.Info("ScreenCapture", $"Final metrics - {metric}");
                }
            }
        }
    }

    private void CaptureScreen(object? state)
    {
        if (!_isCapturing) return;

        // Use Task.Run to avoid blocking the timer thread
        _ = Task.Run(async () =>
        {
            try
            {
                using var timer = _performanceMonitor.TimeOperation("ScreenCapture", "ScreenCapture");
                
                using var bitmap = CaptureDesktop();
                if (bitmap != null)
                {
                    var frame = await ConvertToFrameAsync(bitmap);
                    if (frame != null) // Only send if frame is different enough
                    {
                        FrameCaptured?.Invoke(this, frame);
                        
                        _framesSinceLastReport++;
                        
                        // Report FPS periodically
                        var now = DateTime.UtcNow;
                        if ((now - _lastFpsReport).TotalMilliseconds >= FPS_REPORT_INTERVAL_MS)
                        {
                            var actualFps = _framesSinceLastReport / (now - _lastFpsReport).TotalSeconds;
                            _logger.Info("ScreenCapture", $"Current FPS: {actualFps:F2}, Quality: {_currentQuality}, Similar frames skipped: {_consecutiveSimilarFrames}");
                            
                            _lastFpsReport = now;
                            _framesSinceLastReport = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ScreenCapture", "Screen capture failed", ex);
            }
        });
    }

    private Bitmap? CaptureDesktop()
    {
        try
        {
            using var timer = _performanceMonitor.TimeOperation("CaptureDesktop", "ScreenCapture");
            
            var screenWidth = GetSystemMetrics(0);
            var screenHeight = GetSystemMetrics(1);

            // Optimize for performance - reduce resolution significantly
            var targetWidth = Math.Min(screenWidth, 1280); // Cap at 1280px width (reduced from 1920)
            var targetHeight = Math.Min(screenHeight, 720); // Cap at 720px height (reduced from 1080)
            
            // Calculate scaling factors
            var scaleX = (double)targetWidth / screenWidth;
            var scaleY = (double)targetHeight / screenHeight;
            var scale = Math.Min(scaleX, scaleY);

            var scaledWidth = (int)(screenWidth * scale);
            var scaledHeight = (int)(screenHeight * scale);

            var hdcScreen = GetDC(IntPtr.Zero);
            var hdcMemory = CreateCompatibleDC(hdcScreen);
            var hBitmap = CreateCompatibleBitmap(hdcScreen, scaledWidth, scaledHeight);
            var hOldBitmap = SelectObject(hdcMemory, hBitmap);

            // Use fast scaling instead of high-quality
            SetStretchBltMode(hdcMemory, COLORONCOLOR); // Faster than HALFTONE
            SetBrushOrgEx(hdcMemory, 0, 0, IntPtr.Zero);
            
            StretchBlt(hdcMemory, 0, 0, scaledWidth, scaledHeight, 
                      hdcScreen, 0, 0, screenWidth, screenHeight, SRCCOPY);

            var bitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdcBitmap = graphics.GetHdc();
                BitBlt(hdcBitmap, 0, 0, scaledWidth, scaledHeight, hdcMemory, 0, 0, SRCCOPY);
                graphics.ReleaseHdc(hdcBitmap);
            }

            SelectObject(hdcMemory, hOldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(hdcMemory);
            ReleaseDC(IntPtr.Zero, hdcScreen);

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.Error("ScreenCapture", "Failed to capture desktop", ex);
            return null;
        }
    }

    private async Task<ScreenFrame?> ConvertToFrameAsync(Bitmap bitmap)
    {
        return await Task.Run(() =>
        {
            using var timer = _performanceMonitor.TimeOperation("ConvertToFrame", "ScreenCapture");
            
            var now = DateTime.UtcNow;
            var isKeyFrame = (now - _lastKeyFrame).TotalMilliseconds >= KEY_FRAME_INTERVAL_MS;

            // Compress with current quality setting
            using var ms = new System.IO.MemoryStream();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, _currentQuality);
            
            var jpegCodec = GetEncoder(ImageFormat.Jpeg);
            bitmap.Save(ms, jpegCodec, encoderParams);
            var imageData = ms.ToArray();

            // Frame differencing - temporarily disabled for debugging
            // if (!isKeyFrame && _lastFrameData != null)
            // {
            //     var similarity = CalculateFrameSimilarity(_lastFrameData, imageData);
            //     if (similarity > SIMILARITY_THRESHOLD)
            //     {
            //         _consecutiveSimilarFrames++;
            //         if (_consecutiveSimilarFrames >= SIMILAR_FRAME_THRESHOLD)
            //         {
            //             _logger.Debug("ScreenCapture", $"Skipping similar frame (similarity: {similarity:F2})");
            //             return null; // Skip this frame
            //         }
            //     }
            //     else
            //     {
            //         _consecutiveSimilarFrames = 0;
            //     }
            // }
            // else
            // {
            //     _consecutiveSimilarFrames = 0;
            // }

            if (isKeyFrame)
            {
                _lastKeyFrame = now;
                _logger.Debug("ScreenCapture", "Generating key frame");
            }

            // Store current frame for next comparison
            _lastFrameData = new byte[imageData.Length];
            Array.Copy(imageData, _lastFrameData, imageData.Length);

            var frameNumber = Interlocked.Increment(ref _frameNumber);
            
            _logger.Debug("ScreenCapture", $"Frame {frameNumber}: {bitmap.Width}x{bitmap.Height}, {imageData.Length} bytes, Quality: {_currentQuality}, KeyFrame: {isKeyFrame}");

            return new ScreenFrame
            {
                ImageData = imageData,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Timestamp = now,
                FrameNumber = frameNumber,
                IsKeyFrame = isKeyFrame
            };
        });
    }

    private double CalculateFrameSimilarity(byte[] frame1, byte[] frame2)
    {
        if (frame1.Length != frame2.Length) return 0.0;
        
        // Very efficient similarity check - compare only every 100th byte
        var sampleSize = Math.Max(100, frame1.Length / 100); // Sample every 100th byte
        var differences = 0;
        var totalSamples = 0;
        
        for (int i = 0; i < frame1.Length; i += sampleSize)
        {
            if (i < frame1.Length && i < frame2.Length)
            {
                if (frame1[i] != frame2[i]) differences++;
                totalSamples++;
            }
        }
        
        if (totalSamples == 0) return 1.0;
        return 1.0 - ((double)differences / totalSamples);
    }

    private ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid) ?? codecs[0];
    }

    // Method to adjust quality based on network performance feedback
    public void AdjustQuality(bool networkCongested)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastQualityAdjustment).TotalMilliseconds < QUALITY_ADJUSTMENT_INTERVAL_MS)
            return;

        _lastQualityAdjustment = now;

        if (networkCongested && _currentQuality > MIN_QUALITY)
        {
            _currentQuality = Math.Max(MIN_QUALITY, _currentQuality - 5);
            _logger.Info("ScreenCapture", $"Reduced quality to {_currentQuality} due to network congestion");
        }
        else if (!networkCongested && _currentQuality < MAX_QUALITY)
        {
            _currentQuality = Math.Min(MAX_QUALITY, _currentQuality + 2);
            _logger.Info("ScreenCapture", $"Increased quality to {_currentQuality} due to good network");
        }
    }

    public void Dispose()
    {
        StopCapture();
        _captureTimer?.Dispose();
    }

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest, int nWidthDest, int nHeightDest, IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(IntPtr hdc, int iStretchMode);

    [DllImport("gdi32.dll")]
    private static extern bool SetBrushOrgEx(IntPtr hdc, int nXOrg, int nYOrg, IntPtr lppt);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const uint SRCCOPY = 0x00CC0020;
    private const int HALFTONE = 4;
    private const int COLORONCOLOR = 3; // Added for fast scaling

    #endregion
}
