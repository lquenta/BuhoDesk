using BuhoShared.Models;
using BuhoShared.Network;
using BuhoShared.Services;

Console.WriteLine("Testing BuhoShared assembly...");

try
{
    var chatMessage = new ChatMessage
    {
        SenderId = "test",
        SenderName = "Test",
        Message = "Hello",
        Timestamp = DateTime.UtcNow
    };
    
    Console.WriteLine($"Created ChatMessage: {chatMessage.Message}");
    Console.WriteLine("BuhoShared assembly is working correctly!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
