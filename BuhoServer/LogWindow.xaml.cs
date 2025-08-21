using System.Windows;

namespace BuhoServer;

public partial class LogWindow : Window
{
    public LogWindow(object logger)
    {
        InitializeComponent();
        LogTextBox.Text = "Server Logs - Ready\r\n";
    }
}