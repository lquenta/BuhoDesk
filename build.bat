@echo off
echo ========================================
echo    BuhoDesk Installer Builder
echo ========================================
echo.

echo Building BuhoDesk applications...
powershell -ExecutionPolicy Bypass -File "build-installer.ps1" -Version "1.0.0"

echo.
echo ========================================
echo    Build Complete!
echo ========================================
echo.
echo Output files:
echo - BuhoDesk-Installer\ (folder with applications)
echo - BuhoDesk-v1.0.0.zip (compressed package)
echo.
echo For professional installer:
echo 1. Install Inno Setup from: https://jrsoftware.org/isdl.php
echo 2. Run: "iscc BuhoDesk-Setup.iss"
echo.
pause
