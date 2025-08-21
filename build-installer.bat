@echo off
echo Building BuhoDesk Installer...
echo.

echo Step 1: Cleaning previous builds...
dotnet clean
if %errorlevel% neq 0 (
    echo Error: Failed to clean projects
    pause
    exit /b 1
)

echo Step 2: Building projects in Release mode...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo Error: Failed to build projects
    pause
    exit /b 1
)

echo Step 3: Preparing installer files...
if exist "BuhoDesk-Installer" rmdir /s /q "BuhoDesk-Installer"
mkdir "BuhoDesk-Installer\Server"
mkdir "BuhoDesk-Installer\Client"

echo Step 4: Copying server files...
xcopy "BuhoServer\bin\Release\net9.0-windows\*" "BuhoDesk-Installer\Server\" /E /Y

echo Step 5: Copying client files...
xcopy "BuhoClient\bin\Release\net9.0-windows\*" "BuhoDesk-Installer\Client\" /E /Y

echo Step 6: Copying documentation...
copy "README.md" "BuhoDesk-Installer\README.txt"

echo Step 7: Building installer...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "BuhoDesk-Setup.iss"
if %errorlevel% neq 0 (
    echo Error: Failed to build installer
    pause
    exit /b 1
)

echo.
echo ========================================
echo Installer built successfully!
echo Location: Output\BuhoDesk-Setup-v1.0.0.exe
echo ========================================
echo.
pause
