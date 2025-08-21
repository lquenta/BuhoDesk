using System.Windows;

namespace BuhoClient;

public partial class LogWindow : Window
{
    public LogWindow(object logger)
    {
        InitializeComponent();
        LogTextBox.Text = "Client Logs - Ready\r\n";
    }
}