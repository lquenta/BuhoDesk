# BuhoDesk Installer Build Script
# This script creates a distributable package for BuhoDesk

param(
    [string]$OutputPath = ".\BuhoDesk-Installer",
    [string]$Version = "1.0.0"
)

Write-Host "🚀 Building BuhoDesk Installer v$Version" -ForegroundColor Green

# Clean previous builds
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}

# Create output directory
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\Server" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutputPath\Client" -Force | Out-Null

# Build and publish Server
Write-Host "📦 Building BuhoDesk Server..." -ForegroundColor Yellow
dotnet publish BuhoServer -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "$OutputPath\Server"

# Build and publish Client
Write-Host "📦 Building BuhoDesk Client..." -ForegroundColor Yellow
dotnet publish BuhoClient -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "$OutputPath\Client"

# Create README file
$readmeContent = @"
# BuhoDesk v$Version

## Installation Instructions

### System Requirements
- Windows 10/11 (64-bit)
- No additional .NET runtime required (self-contained)

### Quick Start
1. **Server Setup:**
   - Run `BuhoServer.exe` from the Server folder
   - Click "Start Server" to begin accepting connections
   - Note the IP address displayed

2. **Client Setup:**
   - Run `BuhoClient.exe` from the Client folder
   - Enter the server's IP address and port (default: 8080)
   - Click "Connect" to establish connection

### Features
- Real-time screen sharing
- Remote control (mouse and keyboard)
- Built-in chat system
- Multi-language support (English/Spanish)
- Modern, professional UI

### Language Settings
- Go to File → Language in either application
- Select your preferred language
- Changes apply immediately

### Troubleshooting
- Ensure Windows Firewall allows the applications
- Run as Administrator if screen capture doesn't work
- Check that ports 8080 (TCP) and 8081 (UDP) are available

## Version Information
- Version: $Version
- Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- Target Framework: .NET 9.0
- Architecture: x64

For support, please refer to the project documentation.
"@

$readmeContent | Out-File -FilePath "$OutputPath\README.txt" -Encoding UTF8

# Create batch files for easy launching
$serverBatch = @"
@echo off
echo Starting BuhoDesk Server...
cd /d "%~dp0"
BuhoServer.exe
pause
"@

$clientBatch = @"
@echo off
echo Starting BuhoDesk Client...
cd /d "%~dp0"
BuhoClient.exe
pause
"@

$serverBatch | Out-File -FilePath "$OutputPath\Server\Start-Server.bat" -Encoding ASCII
$clientBatch | Out-File -FilePath "$OutputPath\Client\Start-Client.bat" -Encoding ASCII

# Create desktop shortcuts (optional)
$serverShortcut = @"
@echo off
echo Creating desktop shortcut for BuhoDesk Server...
powershell -Command "`$WshShell = New-Object -comObject WScript.Shell; `$Shortcut = `$WshShell.CreateShortcut(`"`$env:USERPROFILE\Desktop\BuhoDesk Server.lnk`"); `$Shortcut.TargetPath = `"`"%~dp0BuhoServer.exe`"`"; `$Shortcut.Save()"
echo Shortcut created!
pause
"@

$clientShortcut = @"
@echo off
echo Creating desktop shortcut for BuhoDesk Client...
powershell -Command "`$WshShell = New-Object -comObject WScript.Shell; `$Shortcut = `$WshShell.CreateShortcut(`"`$env:USERPROFILE\Desktop\BuhoDesk Client.lnk`"); `$Shortcut.TargetPath = `"`"%~dp0BuhoClient.exe`"`"; `$Shortcut.Save()"
echo Shortcut created!
pause
"@

$serverShortcut | Out-File -FilePath "$OutputPath\Server\Create-Shortcut.bat" -Encoding ASCII
$clientShortcut | Out-File -FilePath "$OutputPath\Client\Create-Shortcut.bat" -Encoding ASCII

# Create ZIP file
Write-Host "📦 Creating ZIP package..." -ForegroundColor Yellow
$zipPath = "BuhoDesk-v$Version.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath

Write-Host "✅ Build completed successfully!" -ForegroundColor Green
Write-Host "📁 Output location: $OutputPath" -ForegroundColor Cyan
Write-Host "📦 ZIP package: $zipPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "🎯 Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the applications in the output folder" -ForegroundColor White
Write-Host "2. Distribute the ZIP file or individual folders" -ForegroundColor White
Write-Host "3. For advanced installer, consider using Inno Setup or NSIS" -ForegroundColor White
