using System.Windows;
using BuhoShared.Services;

namespace BuhoClient;

public partial class LogWindow : Window
{
    private ILogger? _logger;

    public LogWindow(ILogger logger)
    {
        InitializeComponent();
        _logger = logger;
        
        // Subscribe to logger events if available
        if (_logger is Logger concreteLogger)
        {
            concreteLogger.LogEntryAdded += OnLogEntryAdded;
        }
        
        LogTextBox.Text = "Client Logs - Ready\r\n";
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{entry.Timestamp:HH:mm:ss}] {entry.Level} - {entry.Category}: {entry.Message}\r\n");
            LogTextBox.ScrollToEnd();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from events
        if (_logger is Logger concreteLogger)
        {
            concreteLogger.LogEntryAdded -= OnLogEntryAdded;
        }
        base.OnClosed(e);
    }
}