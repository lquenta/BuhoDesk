# BuhoDesk Installer Build Guide

This guide explains how to build the BuhoDesk installer.

## Prerequisites

1. **.NET 9.0 SDK** - Required to build the projects
2. **Inno Setup 6** - Required to create the Windows installer
   - Download from: https://jrsoftware.org/isinfo.php
   - Install to default location: `C:\Program Files (x86)\Inno Setup 6\`

## Quick Build

### Option 1: Using Batch File (Windows)
```cmd
build-installer.bat
```

### Option 2: Using PowerShell Script
```powershell
.\build-installer.ps1
```

### Option 3: Manual Build
```cmd
# 1. Clean and build projects
dotnet clean
dotnet build -c Release

# 2. Create installer directory structure
mkdir BuhoDesk-Installer\Server
mkdir BuhoDesk-Installer\Client

# 3. Copy built files
xcopy "BuhoServer\bin\Release\net9.0-windows\*" "BuhoDesk-Installer\Server\" /E /Y
xcopy "BuhoClient\bin\Release\net9.0-windows\*" "BuhoDesk-Installer\Client\" /E /Y

# 4. Copy documentation
copy "README.md" "BuhoDesk-Installer\README.txt"

# 5. Build installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "BuhoDesk-Setup.iss"
```

## Output

The installer will be created at:
```
Output\BuhoDesk-Setup-v1.0.0.exe
```

## Installer Features

- **Professional Setup Wizard** - Modern UI with Spanish language support
- **Automatic Firewall Rules** - Creates Windows Firewall rules for ports 8080 (TCP) and 8081 (UDP)
- **Desktop Shortcuts** - Optional creation of desktop icons
- **Start Menu Integration** - Adds applications to Windows Start Menu
- **Admin Privileges** - Requires administrator rights for proper installation
- **64-bit Support** - Optimized for 64-bit Windows systems

## Installation Contents

The installer includes:
- **BuhoServer** - Remote desktop server application
- **BuhoClient** - Remote desktop client application
- **BuhoShared** - Shared libraries and resources
- **Documentation** - README and license files

## Troubleshooting

### Build Errors
- Ensure .NET 9.0 SDK is installed
- Run `dotnet --version` to verify
- Clean solution with `dotnet clean` before rebuilding

### Installer Errors
- Ensure Inno Setup 6 is installed in the default location
- Check that all source files exist in the expected locations
- Verify that the build completed successfully before creating installer

### Runtime Errors
- Ensure .NET 9.0 Runtime is installed on target machines
- Check Windows Firewall settings
- Verify ports 8080 and 8081 are not blocked

## Version Information

- **Current Version**: 1.0.0
- **Target Framework**: .NET 9.0
- **Platform**: Windows x64
- **Architecture**: 64-bit

## License

This installer is part of the BuhoDesk project. See LICENSE.txt for details.
