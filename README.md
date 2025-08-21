# BuhoDesk

A remote desktop application built with .NET 9 and WPF, featuring real-time screen sharing, remote control, and chat functionality.

## Features

- **Real-time Screen Sharing**: View remote desktop in real-time with high performance
- **Remote Control**: Full mouse and keyboard control of remote machine
- **Chat System**: Built-in chat functionality for communication between client and server
- **Multiple Client Support**: Server can handle multiple simultaneous connections
- **UDP Screen Broadcasting**: High-performance screen frame transmission
- **TCP Control Channel**: Reliable control and chat message transmission
- **Modern UI**: Clean, modern WPF interface with Material Design styling

## Chat Functionality

The application now includes a comprehensive chat system that allows:

- **Real-time messaging** between server and connected clients
- **Message history** with timestamps and sender information
- **Auto-scrolling** chat display
- **Enter key support** for quick message sending
- **Broadcast messaging** from server to all connected clients
- **Individual client identification** with custom client names

### How to Use Chat

1. **Server Side**: 
   - Start the server application
   - The chat panel appears at the bottom of the server window
   - Type messages in the input box and press Enter or click Send
   - Messages are broadcast to all connected clients

2. **Client Side**:
   - Connect to the server
   - The chat panel appears on the right side of the client window
   - Type messages in the input box and press Enter or click Send
   - Messages are sent to the server and broadcast to all other clients

## Projects

- **BuhoServer**: Main server application with screen capture and input simulation
- **BuhoClient**: Modern client application with enhanced UI
- **BuhoShared**: Shared models and network protocols

## Network Architecture

- **TCP (Port 8080)**: Control channel for mouse/keyboard events and chat messages
- **UDP (Port 8081)**: High-performance screen frame broadcasting
- **JSON Protocol**: Structured message format for all communications

## Building and Running

1. Ensure you have .NET 9 SDK installed
2. Clone the repository
3. Run `dotnet build` to build all projects
4. Start the server: `dotnet run --project BuhoServer`
5. Start one or more clients: `dotnet run --project BuhoClient`

## Usage

1. **Start the Server**:
   - Launch BuhoServer
   - Click "Start Server"
   - Note the IP address and port displayed

2. **Connect Clients**:
   - Launch BuhoClient
   - Enter the server's IP address and port (default: 8080)
   - Set a custom client name (optional)
   - Click "Connect"

3. **Use Chat**:
   - Once connected, use the chat panel to send messages
   - Messages are displayed with sender name and timestamp
   - Press Enter to send messages quickly

4. **Remote Control**:
   - Click and drag on the remote desktop image to control the remote machine
   - Use keyboard input when the remote desktop image has focus

## Technical Details

- **Screen Capture**: Uses Windows Graphics Capture API for high-performance screen recording
- **Input Simulation**: Windows Input Simulation for remote control
- **Network Protocol**: Custom JSON-based protocol over TCP/UDP
- **UI Framework**: WPF with modern styling and responsive design
- **Logging**: Comprehensive logging system for debugging and monitoring

## Requirements

- Windows 10/11
- .NET 9 Runtime
- Administrative privileges (for screen capture and input simulation)
