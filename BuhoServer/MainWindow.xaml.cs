using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using BuhoServer.Services;
using BuhoShared.Services;
using BuhoShared.Models;
using BuhoShared.Network;

namespace BuhoServer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly NetworkServerService _networkService;
    private readonly UdpScreenService _udpScreenService;
    private readonly ScreenCaptureService _screenService;
    private readonly InputSimulationService _inputService;
    private readonly TaskbarNotificationService _taskbarNotification;
    private readonly ObservableCollection<string> _connectedClients;
    private readonly ObservableCollection<ChatMessage> _chatMessages;
    private readonly ILogger _logger;
    private const int SERVER_PORT = 8080;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            
            _logger = new Logger();
            _inputService = new InputSimulationService();
            _screenService = new ScreenCaptureService(_logger);
            _networkService = new NetworkServerService(SERVER_PORT, _inputService, _screenService, _logger);
            _udpScreenService = new UdpScreenService(_logger);
            _taskbarNotification = new TaskbarNotificationService(this);
            
            _connectedClients = new ObservableCollection<string>();
            _chatMessages = new ObservableCollection<ChatMessage>();
            ClientsListBox.ItemsSource = _connectedClients;
            ChatMessagesListBox.ItemsSource = _chatMessages;
            
            _networkService.ClientConnected += OnClientConnected;
            _networkService.ClientDisconnected += OnClientDisconnected;
            _networkService.ChatMessageReceived += OnChatMessageReceived;
            _udpScreenService.ClientConnected += OnUdpClientConnected;
            _udpScreenService.ClientDisconnected += OnUdpClientDisconnected;
            _screenService.FrameCaptured += OnFrameCaptured;
            
            // Subscribe to language changes
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            
            _logger.Info("ServerUI", "Buho Server application started");
            UpdateServerInfo();
            UpdateLocalizedStrings();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _networkService.Start();
            _udpScreenService.Start();
            _screenService.StartCapture();
            
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusText.Text = "Server Status: Running (TCP + UDP)";
            StatusIndicator.Fill = System.Windows.Media.Brushes.Green;
            
            UpdateServerInfo();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _networkService.Stop();
            _udpScreenService.Stop();
            _screenService.StopCapture();
            
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "Server Status: Stopped";
            StatusIndicator.Fill = System.Windows.Media.Brushes.Red;
            
            UpdateConnectionCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClientConnected(object? sender, string clientId)
    {
        Dispatcher.Invoke(() =>
        {
            _connectedClients.Add(clientId);
            UpdateConnectionCount();
            _logger.Info("ServerUI", $"Client connected: {clientId}");
            
            // Alert user with taskbar flash for new client connection
            if (!IsActive)
            {
                _taskbarNotification.AlertUser(3, 400); // Flash 3 times, every 400ms
            }
        });
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        Dispatcher.Invoke(() =>
        {
            _connectedClients.Remove(clientId);
            UpdateConnectionCount();
            _logger.Info("ServerUI", $"Client disconnected: {clientId}");
        });
    }

    private void OnUdpClientConnected(object? sender, string clientId)
    {
        _logger.Info("ServerUI", $"UDP client connected: {clientId}");
    }

    private void OnUdpClientDisconnected(object? sender, string clientId)
    {
        _logger.Info("ServerUI", $"UDP client disconnected: {clientId}");
    }

    private async void OnFrameCaptured(object? sender, ScreenFrame frame)
    {
        await _udpScreenService.BroadcastFrameAsync(frame);
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
            
            // Alert user with taskbar flash if window is not active
            if (!IsActive)
            {
                _taskbarNotification.AlertUser(5, 300); // Flash 5 times, every 300ms
                _logger.Info("ServerUI", $"Chat message received from {chatMessage.SenderName}: {chatMessage.Message}");
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
            await _networkService.SendChatMessageAsync(message, "Server");
            ChatInputTextBox.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error("ServerUI", $"Failed to send chat message: {ex.Message}");
            MessageBox.Show($"Failed to send chat message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }



    private void UpdateServerInfo()
    {
        ServerIdText.Text = _networkService.ServerId;
        PortText.Text = SERVER_PORT.ToString();
        
        try
        {
            var hostName = Dns.GetHostName();
            var hostEntry = Dns.GetHostEntry(hostName);
            var localIp = hostEntry.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            
            IpAddressText.Text = localIp?.ToString() ?? "Unknown";
        }
        catch
        {
            IpAddressText.Text = "Unknown";
        }
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
            $"BuhoDesk Server\nVersion 1.0\n\n{loc.GetString("About")}\n\nLeonardo Quenta - 2025\nFrom Bolivia with love ❤️",
            loc.GetString("About"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from language changes
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        
        _taskbarNotification?.Dispose();
        _networkService?.Dispose();
        _screenService?.Dispose();
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
        Title = loc.GetString("ServerTitle");
        
        // Update status text
        if (StatusText.Text.Contains("Running"))
        {
            StatusText.Text = loc.GetString("ServerStatusRunning");
        }
        else
        {
            StatusText.Text = loc.GetString("ServerStatusStopped");
        }
        
        // Update button texts
        StartButton.Content = loc.GetString("StartServer");
        StopButton.Content = loc.GetString("StopServer");
        LogsButton.Content = loc.GetString("ViewLogs");
        SendChatButton.Content = loc.GetString("Send");
        
        // Update connection count
        UpdateConnectionCount();
    }

    private void UpdateConnectionCount()
    {
        var loc = LocalizationService.Instance;
        ConnectionCount.Text = loc.GetString("ConnectedClientsCount", _connectedClients.Count);
    }
}