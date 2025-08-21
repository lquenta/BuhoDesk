using System.Runtime.InteropServices;
using BuhoShared.Models;

namespace BuhoServer.Services;

public class InputSimulationService
{
    public void SimulateMouseEvent(MouseEvent mouseEvent)
    {
        try
        {
            switch (mouseEvent.EventType)
            {
                case MouseEventType.Move:
                    SetCursorPos(mouseEvent.X, mouseEvent.Y);
                    break;
                case MouseEventType.Down:
                    SetCursorPos(mouseEvent.X, mouseEvent.Y);
                    switch (mouseEvent.Button)
                    {
                        case MouseButton.Left:
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                            break;
                        case MouseButton.Right:
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                            break;
                        case MouseButton.Middle:
                            mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                            break;
                    }
                    break;
                case MouseEventType.Up:
                    SetCursorPos(mouseEvent.X, mouseEvent.Y);
                    switch (mouseEvent.Button)
                    {
                        case MouseButton.Left:
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                            break;
                        case MouseButton.Right:
                            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                            break;
                        case MouseButton.Middle:
                            mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                            break;
                    }
                    break;
                case MouseEventType.Scroll:
                    SetCursorPos(mouseEvent.X, mouseEvent.Y);
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)(mouseEvent.ScrollDelta * 120), 0);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error simulating mouse event: {ex.Message}");
        }
    }

    public void SimulateKeyboardEvent(KeyboardEvent keyboardEvent)
    {
        try
        {
            if (keyboardEvent.IsKeyDown)
            {
                if (keyboardEvent.IsCtrlPressed) keybd_event(VK_CONTROL, 0, 0, 0);
                if (keyboardEvent.IsAltPressed) keybd_event(VK_MENU, 0, 0, 0);
                if (keyboardEvent.IsShiftPressed) keybd_event(VK_SHIFT, 0, 0, 0);
                
                keybd_event((byte)keyboardEvent.KeyCode, 0, 0, 0);
            }
            else
            {
                keybd_event((byte)keyboardEvent.KeyCode, 0, KEYEVENTF_KEYUP, 0);
                
                if (keyboardEvent.IsCtrlPressed) keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                if (keyboardEvent.IsAltPressed) keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
                if (keyboardEvent.IsShiftPressed) keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error simulating keyboard event: {ex.Message}");
        }
    }

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, uint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    
    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_MENU = 0x12;

    #endregion
}
