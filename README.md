# BuhoDesk

A professional remote desktop application built with .NET 9 and WPF, featuring real-time screen sharing, remote control, network discovery, and comprehensive communication tools.

## 🎯 Motivation

BuhoDesk was born from the need for a **free, open-source alternative** to commercial remote desktop solutions. In today's digital landscape, many users find themselves dependent on expensive subscription-based services that often compromise privacy and data security.

### Why BuhoDesk?

- **🆓 Completely Free**: No subscriptions, no hidden costs, no feature limitations
- **🔒 Privacy-First**: Your data stays on your network - no cloud storage or third-party servers
- **🚫 No Data Mining**: Unlike commercial alternatives, we don't collect, sell, or monetize your data
- **⚡ Self-Hosted**: Full control over your remote desktop infrastructure
- **🛠️ Open Source**: Transparent codebase that you can inspect, modify, and contribute to
- **🎯 Focused Functionality**: Essential remote desktop features without bloat or unnecessary complexity

### The Problem with Commercial Alternatives

Commercial remote desktop applications often:
- **Lock you into expensive subscriptions** for basic functionality
- **Collect and sell your usage data** to third parties
- **Require internet connectivity** even for local network usage
- **Limit features** based on subscription tiers
- **Create vendor lock-in** with proprietary protocols

BuhoDesk provides a **professional-grade solution** that puts you back in control of your remote desktop needs without compromising on privacy, cost, or functionality.

## 🚀 Version 1.0.0 Release

**BuhoDesk v1.0.0** is now available with complete remote desktop functionality including mouse cursor rendering, network discovery, taskbar notifications, and multi-language support.

### 📦 Download
- **Installer**: `BuhoDesk-Setup-v1.0.0.exe` (2.0 MB)
- **Requirements**: Windows 10/11, .NET 9.0 Runtime
- **Architecture**: x64

## ✨ Features

### 🖥️ Core Functionality
- **Real-time Screen Sharing**: High-performance desktop streaming with adaptive quality
- **Mouse Cursor Rendering**: Visible mouse pointer in remote desktop sessions
- **Remote Control**: Full mouse and keyboard control with modifier key support
- **Network Discovery**: Automatic server detection on local network
- **Chat System**: Built-in messaging with real-time communication

### 🎯 Advanced Features
- **Taskbar Notifications**: Visual alerts for connections and messages
- **Multi-language Support**: English and Spanish interfaces
- **Performance Optimization**: Frame differencing and adaptive compression
- **Firewall Integration**: Automatic port configuration
- **Multiple Client Support**: Server handles multiple simultaneous connections

### 🔧 Technical Features
- **TCP/UDP Communication**: Reliable control + fast screen streaming
- **Frame Differencing**: Only sends changed screen regions
- **Adaptive Quality**: Dynamic JPEG compression based on network conditions
- **UDP Screen Broadcasting**: High-performance frame transmission
- **JSON Protocol**: Structured message format for all communications

## 🏗️ Architecture

### Network Protocol
- **TCP (Port 8080)**: Control channel for mouse/keyboard events and chat messages
- **UDP (Port 8081)**: High-performance screen frame broadcasting
- **UDP (Port 8082)**: Network discovery service

### Projects Structure
- **BuhoServer**: Main server application with screen capture and input simulation
- **BuhoClient**: Modern client application with enhanced UI and discovery
- **BuhoShared**: Shared models, network protocols, and services

## 🎮 How to Use

### 1. Installation
1. Download and run `BuhoDesk-Setup-v1.0.0.exe`
2. Follow the installation wizard
3. Allow firewall rules when prompted

### 2. Starting the Server
1. Launch **BuhoServer** from Start Menu or Desktop
2. Click **"Start Server"**
3. Server will start listening on ports 8080 (TCP), 8081 (UDP), and 8082 (Discovery)
4. Note the status indicator turns green when running

### 3. Connecting Clients
#### Option A: Network Discovery (Recommended)
1. Launch **BuhoClient**
2. Click **"Discover Servers"**
3. Select a server from the dropdown list
4. Click **"Connect"**

#### Option B: Manual Connection
1. Launch **BuhoClient**
2. Enter server IP address manually
3. Set port (default: 8080)
4. Click **"Connect"**

### 4. Remote Control
- **Mouse Control**: Click and drag on remote desktop image
- **Keyboard Input**: Type when remote desktop has focus
- **Mouse Cursor**: Visible pointer shows current position
- **Modifier Keys**: Ctrl, Alt, Shift combinations work

### 5. Chat Communication
- **Send Messages**: Type in chat input box and press Enter
- **Real-time**: Messages appear instantly with timestamps
- **Notifications**: Taskbar flashes for new messages

## 🔧 Technical Details

### Screen Capture Technology
- **Win32 API Integration**: Direct screen capture using `BitBlt` and `StretchBlt`
- **Mouse Cursor Rendering**: `GetCursorInfo` and `DrawIcon` for cursor display
- **Performance Optimization**: Configurable capture frequency and resolution
- **Frame Differencing**: Intelligent change detection to reduce bandwidth

### Input Simulation
- **Mouse Events**: Movement, clicks, scrolling with proper scaling
- **Keyboard Events**: Full key support including modifier combinations
- **Win32 API**: Direct input simulation using `mouse_event` and `keybd_event`

### Network Services
- **NetworkDiscoveryService**: UDP broadcast for automatic server discovery
- **TaskbarNotificationService**: Visual alerts using `FlashWindow`
- **LocalizationService**: Multi-language support with .resx files

### Performance Features
- **Adaptive Quality**: Dynamic JPEG compression (40-60 quality range)
- **Frame Skipping**: Intelligent frame differencing to reduce CPU usage
- **UDP Chunking**: Efficient screen frame transmission in chunks
- **Background Processing**: Non-blocking operations for responsive UI

## 📋 Requirements

### System Requirements
- **OS**: Windows 10/11 (x64)
- **Runtime**: .NET 9.0 Runtime
- **Privileges**: Administrative (for screen capture and input simulation)
- **Network**: Local network access for discovery and communication

### Development Requirements
- **SDK**: .NET 9.0 SDK
- **IDE**: Visual Studio 2022 or VS Code
- **Tools**: Inno Setup 6 (for installer creation)

## 🛠️ Building from Source

### Prerequisites
```bash
# Install .NET 9.0 SDK
# Clone the repository
git clone https://github.com/your-repo/buhodesk.git
cd buhodesk
```

### Build Commands
```bash
# Build all projects
dotnet build BuhoDesk.sln

# Build specific projects
dotnet build BuhoServer/BuhoServer.csproj
dotnet build BuhoClient/BuhoClient.csproj
dotnet build BuhoShared/BuhoShared.csproj

# Run applications
dotnet run --project BuhoServer/BuhoServer.csproj
dotnet run --project BuhoClient/BuhoClient.csproj
```

### Creating Installer
```bash
# Build release version
dotnet build BuhoDesk.sln --configuration Release

# Create installer (requires Inno Setup 6)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "BuhoDesk-Setup.iss"
```

## 🔒 Security & Privacy

### Network Security
- **Local Network Only**: Designed for trusted local network environments
- **No Internet Access**: All communication stays within local network
- **Firewall Integration**: Automatic port configuration for security

### Data Handling
- **No Data Storage**: Screen captures are not saved to disk
- **Memory Only**: All processing happens in memory
- **No Logging**: No sensitive data is logged or transmitted

## 🐛 Troubleshooting

### Common Issues
1. **Connection Failed**: Check firewall settings and ensure server is running
2. **No Servers Found**: Verify network discovery is enabled and servers are running
3. **Mouse Not Visible**: Ensure you're using v1.0.0+ for cursor rendering
4. **Performance Issues**: Adjust capture frequency in server settings

### Logs and Debugging
- **Log Files**: Located in `C:\Users\[username]\Desktop\AnyDeskClone\Logs\`
- **Log Viewer**: Available in both server and client applications
- **Performance Monitor**: Built-in metrics for optimization

## 📝 Changelog

### v1.0.0 (Current Release)
- ✅ **Mouse Cursor Rendering**: Visible mouse pointer in remote sessions
- ✅ **Network Discovery**: Automatic server detection on local network
- ✅ **Taskbar Notifications**: Visual alerts for connections and messages
- ✅ **Multi-language Support**: English and Spanish interfaces
- ✅ **UI Improvements**: Responsive chat interface and language selection
- ✅ **Performance Optimizations**: Reduced CPU usage and improved FPS
- ✅ **Installer**: Professional Windows installer with firewall integration

### Previous Versions
- **v0.9.0**: Initial release with basic remote desktop functionality
- **v0.8.0**: Added chat system and performance improvements
- **v0.7.0**: UDP screen streaming and frame differencing

## 🤝 Contributing

We welcome contributions! Please see our contributing guidelines for details.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For support and questions:
- **Issues**: Create an issue on GitHub
- **Documentation**: Check the built-in help and logs
- **Community**: Join our community discussions

---

## 👨‍💻 Author

**BuhoDesk** is proudly created by **Leonardo Quenta Alarcón**, a passionate developer from **La Paz, Bolivia** 🇧🇴.

### 🏔️ High Altitude Development

As a **proud high-altitude developer** with an even higher attitude, I bring a unique perspective to software development from the heart of the Andes Mountains. Working at 3,650 meters above sea level (11,975 feet) in one of the world's highest capital cities, I've learned that great software can be built anywhere - even where the air is thin but the ideas are bold! 😄

### 🌟 Why This Matters

- **Global Perspective**: Software development knows no geographical boundaries
- **Innovation from Unlikely Places**: Great ideas can come from anywhere in the world
- **Community Building**: Open source connects developers across continents
- **Cultural Diversity**: Different perspectives lead to better solutions

### 🤝 Connect

Feel free to reach out and connect with a developer who literally has his head in the clouds! ☁️

---

**BuhoDesk** - Professional remote desktop solution for Windows
