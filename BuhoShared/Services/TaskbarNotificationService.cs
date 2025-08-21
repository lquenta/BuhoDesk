using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace BuhoShared.Services;

/// <summary>
/// Service for taskbar notifications and window flashing
/// </summary>
public class TaskbarNotificationService
{
    private readonly DispatcherTimer _flashTimer;
    private readonly Window _window;
    private bool _isFlashing = false;
    private int _flashCount = 0;
    private const int MAX_FLASH_COUNT = 10; // Flash 10 times by default

    // Win32 API for flashing window
    [DllImport("user32.dll")]
    private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    public TaskbarNotificationService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        
        _flashTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Flash every 500ms
        };
        _flashTimer.Tick += FlashTimer_Tick;
    }

    /// <summary>
    /// Flashes the window in the taskbar to alert the user
    /// </summary>
    /// <param name="flashCount">Number of times to flash (default: 10)</param>
    /// <param name="intervalMs">Interval between flashes in milliseconds (default: 500)</param>
    public void FlashWindow(int flashCount = MAX_FLASH_COUNT, int intervalMs = 500)
    {
        if (_isFlashing)
        {
            StopFlashing();
        }

        _flashCount = 0;
        _flashTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        _isFlashing = true;
        _flashTimer.Start();
    }

    /// <summary>
    /// Stops the current flashing animation
    /// </summary>
    public void StopFlashing()
    {
        if (_isFlashing)
        {
            _flashTimer.Stop();
            _isFlashing = false;
            _flashCount = 0;
            
            // Ensure window is not flashing when stopped
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
                if (handle != IntPtr.Zero)
                {
                    FlashWindow(handle, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TaskbarNotificationService StopFlashing error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Brings the window to the front and activates it
    /// </summary>
    public void BringToFront()
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    /// <summary>
    /// Flashes the window and brings it to front
    /// </summary>
    /// <param name="flashCount">Number of times to flash</param>
    /// <param name="intervalMs">Interval between flashes</param>
    public void AlertUser(int flashCount = MAX_FLASH_COUNT, int intervalMs = 500)
    {
        FlashWindow(flashCount, intervalMs);
        BringToFront();
    }

    private void FlashTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isFlashing || _flashCount >= MAX_FLASH_COUNT)
        {
            StopFlashing();
            return;
        }

        try
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
            if (handle != IntPtr.Zero)
            {
                FlashWindow(handle, true);
                _flashCount++;
            }
            else
            {
                // Window handle not available yet, stop flashing
                StopFlashing();
            }
        }
        catch (Exception ex)
        {
            // Log error and stop flashing
            System.Diagnostics.Debug.WriteLine($"TaskbarNotificationService error: {ex.Message}");
            StopFlashing();
        }
    }

    /// <summary>
    /// Disposes the service and stops any ongoing flashing
    /// </summary>
    public void Dispose()
    {
        StopFlashing();
        _flashTimer?.Stop();
    }
}
