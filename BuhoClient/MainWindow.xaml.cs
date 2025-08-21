using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BuhoClient.Services;
using BuhoShared.Models;
using BuhoShared.Network;
using BuhoShared.Services;

namespace BuhoClient;

public partial class MainWindow : Window
{
    private readonly NetworkClientService _networkService;
    private readonly UdpScreenClientService _udpScreenService;
    private readonly ILogger _logger;
    private readonly ObservableCollection<ChatMessage> _chatMessages;
    private int _frameCount = 0;
    private DateTime _lastFrameTime = DateTime.MinValue;
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;
    private int _originalWidth = 0;
    private int _originalHeight = 0;

    public MainWindow()
    {
        InitializeComponent();
        
        _logger = new Logger();
        _networkService = new NetworkClientService(_logger);
        _udpScreenService = new UdpScreenClientService(_logger, _networkService.ClientId);
        _chatMessages = new ObservableCollection<ChatMessage>();
        
        _networkService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _networkService.ConnectionResponseReceived += OnConnectionResponseReceived;
        _networkService.FrameReceived += OnFrameReceived;
        _networkService.ChatMessageReceived += OnChatMessageReceived;
        _udpScreenService.FrameReceived += OnUdpFrameReceived;
        
        // Subscribe to language changes
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        
        // Set up chat UI
        ChatMessagesListBox.ItemsSource = _chatMessages;
        
        // Set focus to the image for keyboard events
        RemoteDesktopImage.Focusable = true;
        RemoteDesktopImage.KeyDown += RemoteDesktopImage_KeyDown;
        RemoteDesktopImage.KeyUp += RemoteDesktopImage_KeyUp;
        
        _logger.Info("ClientUI", "Buho Client application started");
        UpdateLocalizedStrings();
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var serverAddress = ServerAddressTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(serverAddress))
            {
                MessageBox.Show("Please enter a server address.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text?.Trim(), out int port))
            {
                MessageBox.Show("Please enter a valid port number.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _networkService.ConnectAsync(serverAddress, port);
            
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;
            ConnectionStatusText.Text = "Connecting...";
            ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Yellow;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to connect: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _networkService.Disconnect();
            
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            ConnectionStatusText.Text = "Disconnected";
            ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
            
            // Clear the remote desktop image
            RemoteDesktopImage.Source = null;
            FrameInfoText.Text = "No connection established";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to disconnect: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnConnectionStatusChanged(object? sender, string status)
    {
        Dispatcher.Invoke(() =>
        {
            ConnectionStatusText.Text = status;
            
            if (status.Contains("Connected"))
            {
                ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Green;
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
            }
            else if (status.Contains("Disconnected"))
            {
                ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
            }
            else if (status.Contains("Connecting"))
            {
                ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Yellow;
            }
        });
    }

    private async void OnConnectionResponseReceived(object? sender, ConnectionResponse response)
    {
        await Dispatcher.Invoke(async () =>
        {
            if (response.Success)
            {
                _logger.Info("ClientUI", $"Connected to server: {response.ServerId}");
                FrameInfoText.Text = $"Connected to {response.ServerId}";
                
                // Start UDP screen client and register with server
                try
                {
                    _udpScreenService.Start();
                    
                    // Get the server address from the connection
                    var serverAddress = ServerAddressTextBox.Text?.Trim();
                    if (!string.IsNullOrEmpty(serverAddress))
                    {
                        var success = await _udpScreenService.RegisterWithServerAsync(serverAddress, 8081); // UDP port
                        if (success)
                        {
                            _logger.Info("ClientUI", "UDP screen client registered successfully");
                        }
                        else
                        {
                            _logger.Warning("ClientUI", "Failed to register UDP screen client");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("ClientUI", $"Failed to start UDP screen client: {ex.Message}");
                }
            }
            else
            {
                _logger.Error("ClientUI", $"Connection failed: {response.Message}");
                MessageBox.Show($"Connection failed: {response.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                ConnectionStatusText.Text = "Connection Failed";
                ConnectionStatusIndicator.Fill = System.Windows.Media.Brushes.Red;
            }
        });
    }

    private void OnFrameReceived(object? sender, ScreenFrame frame)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var bitmap = ConvertBytesToBitmap(frame.ImageData);
                var bitmapSource = ConvertBitmapToBitmapSource(bitmap);
                
                RemoteDesktopImage.Source = bitmapSource;
                
                _originalWidth = frame.Width;
                _originalHeight = frame.Height;
                
                _frameCount++;
                var now = DateTime.Now;
                var fps = 1.0 / (now - _lastFrameTime).TotalSeconds;
                _lastFrameTime = now;
                
                FrameInfoText.Text = $"Frame {_frameCount} - {frame.Width}x{frame.Height} - FPS: {fps:F1}";
            }
            catch (Exception ex)
            {
                _logger.Error("ClientUI", $"Failed to process frame: {ex.Message}");
            }
        });
    }

    private void OnUdpFrameReceived(object? sender, ScreenFrame frame)
    {
        // Handle UDP frames the same way as TCP frames
        OnFrameReceived(sender, frame);
    }

    private void OnChatMessageReceived(object? sender, ChatMessage chatMessage)
    {
        Dispatcher.Invoke(() =>
        {
            _chatMessages.Add(chatMessage);
            
            // Auto-scroll to the latest message
            if (ChatMessagesListBox.Items.Count > 0)
            {
                ChatMessagesListBox.ScrollIntoView(ChatMessagesListBox.Items[ChatMessagesListBox.Items.Count - 1]);
            }
        });
    }

    private async void SendChatButton_Click(object sender, RoutedEventArgs e)
    {
        await SendChatMessage();
    }

    private async void ChatInputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            await SendChatMessage();
            e.Handled = true;
        }
    }

    private async Task SendChatMessage()
    {
        var message = ChatInputTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            var clientName = ClientNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(clientName))
                clientName = "Client";

            await _networkService.SendChatMessageAsync(message, clientName);
            ChatInputTextBox.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error("ClientUI", $"Failed to send chat message: {ex.Message}");
            MessageBox.Show($"Failed to send chat message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Bitmap ConvertBytesToBitmap(byte[] imageData)
    {
        using var stream = new MemoryStream(imageData);
        return new Bitmap(stream);
    }

    private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private void RemoteDesktopImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_networkService.IsConnected || _originalWidth == 0) return;
        
        var position = e.GetPosition(RemoteDesktopImage);
        var actualPosition = e.GetPosition(RemoteDesktopImage);
        
        // Calculate scale factors
        var imageWidth = RemoteDesktopImage.ActualWidth;
        var imageHeight = RemoteDesktopImage.ActualHeight;
        
        _scaleX = _originalWidth / imageWidth;
        _scaleY = _originalHeight / imageHeight;
        
        var scaledX = (int)(actualPosition.X * _scaleX);
        var scaledY = (int)(actualPosition.Y * _scaleY);
        
        var mouseEvent = new MouseEvent
        {
            X = scaledX,
            Y = scaledY,
            EventType = MouseEventType.Move,
            Button = BuhoShared.Models.MouseButton.Left
        };
        
        _ = _networkService.SendMouseEventAsync(mouseEvent);
    }

    private void RemoteDesktopImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_networkService.IsConnected || _originalWidth == 0) return;
        
        var position = e.GetPosition(RemoteDesktopImage);
        var scaledX = (int)(position.X * _scaleX);
        var scaledY = (int)(position.Y * _scaleY);
        
        var button = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.Left => BuhoShared.Models.MouseButton.Left,
            System.Windows.Input.MouseButton.Right => BuhoShared.Models.MouseButton.Right,
            System.Windows.Input.MouseButton.Middle => BuhoShared.Models.MouseButton.Middle,
            _ => BuhoShared.Models.MouseButton.Left
        };
        
        var mouseEvent = new MouseEvent
        {
            X = scaledX,
            Y = scaledY,
            EventType = MouseEventType.Down,
            Button = button
        };
        
        _ = _networkService.SendMouseEventAsync(mouseEvent);
    }

    private void RemoteDesktopImage_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_networkService.IsConnected || _originalWidth == 0) return;
        
        var position = e.GetPosition(RemoteDesktopImage);
        var scaledX = (int)(position.X * _scaleX);
        var scaledY = (int)(position.Y * _scaleY);
        
        var button = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.Left => BuhoShared.Models.MouseButton.Left,
            System.Windows.Input.MouseButton.Right => BuhoShared.Models.MouseButton.Right,
            System.Windows.Input.MouseButton.Middle => BuhoShared.Models.MouseButton.Middle,
            _ => BuhoShared.Models.MouseButton.Left
        };
        
        var mouseEvent = new MouseEvent
        {
            X = scaledX,
            Y = scaledY,
            EventType = MouseEventType.Up,
            Button = button
        };
        
        _ = _networkService.SendMouseEventAsync(mouseEvent);
    }

    private void RemoteDesktopImage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_networkService.IsConnected || _originalWidth == 0) return;
        
        var position = e.GetPosition(RemoteDesktopImage);
        var scaledX = (int)(position.X * _scaleX);
        var scaledY = (int)(position.Y * _scaleY);
        
        var mouseEvent = new MouseEvent
        {
            X = scaledX,
            Y = scaledY,
            EventType = MouseEventType.Scroll,
            ScrollDelta = e.Delta > 0 ? 1 : -1
        };
        
        _ = _networkService.SendMouseEventAsync(mouseEvent);
    }

    private void RemoteDesktopImage_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_networkService.IsConnected) return;
        
        var keyboardEvent = new KeyboardEvent
        {
            KeyCode = (int)KeyInterop.VirtualKeyFromKey(e.Key),
            IsKeyDown = true,
            IsCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
            IsAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt),
            IsShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
        };
        
        _ = _networkService.SendKeyboardEventAsync(keyboardEvent);
    }

    private void RemoteDesktopImage_KeyUp(object sender, KeyEventArgs e)
    {
        if (!_networkService.IsConnected) return;
        
        var keyboardEvent = new KeyboardEvent
        {
            KeyCode = (int)KeyInterop.VirtualKeyFromKey(e.Key),
            IsKeyDown = false,
            IsCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
            IsAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt),
            IsShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
        };
        
        _ = _networkService.SendKeyboardEventAsync(keyboardEvent);
    }

    private void LogsButton_Click(object sender, RoutedEventArgs e)
    {
        var logWindow = new LogWindow(_logger);
        logWindow.Show();
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var languageWindow = new BuhoShared.Windows.LanguageSelectionWindow();
        if (languageWindow.ShowDialog() == true)
        {
            LocalizationService.Instance.SetLanguage(languageWindow.SelectedLanguage);
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        MessageBox.Show(
            $"BuhoDesk Client\nVersion 1.0\n\n{loc.GetString("About")}",
            loc.GetString("About"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from language changes
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        
        _networkService?.Dispose();
        base.OnClosed(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateLocalizedStrings);
    }

    private void UpdateLocalizedStrings()
    {
        var loc = LocalizationService.Instance;
        
        // Update window title
        Title = loc.GetString("ClientTitle");
        
        // Update button texts
        ConnectButton.Content = loc.GetString("Connect");
        DisconnectButton.Content = loc.GetString("Disconnect");
        LogsButton.Content = loc.GetString("ViewLogs");
        SendChatButton.Content = loc.GetString("Send");
        
        // Update status texts
        if (ConnectionStatusText.Text.Contains("Connected"))
        {
            ConnectionStatusText.Text = loc.GetString("Connected");
        }
        else if (ConnectionStatusText.Text.Contains("Connecting"))
        {
            ConnectionStatusText.Text = loc.GetString("Connecting");
        }
        else if (ConnectionStatusText.Text.Contains("Disconnected"))
        {
            ConnectionStatusText.Text = loc.GetString("Disconnected");
        }
        else if (ConnectionStatusText.Text.Contains("Failed"))
        {
            ConnectionStatusText.Text = loc.GetString("ConnectionFailed");
        }
        else
        {
            ConnectionStatusText.Text = loc.GetString("NoConnection");
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
