# BuhoDesk Installer Build Script
Write-Host "Building BuhoDesk Installer..." -ForegroundColor Green
Write-Host ""

# Step 1: Clean previous builds
Write-Host "Step 1: Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to clean projects" -ForegroundColor Red
    Read-Host "Press Enter to continue"
    exit 1
}

# Step 2: Build projects in Release mode
Write-Host "Step 2: Building projects in Release mode..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to build projects" -ForegroundColor Red
    Read-Host "Press Enter to continue"
    exit 1
}

# Step 3: Prepare installer files
Write-Host "Step 3: Preparing installer files..." -ForegroundColor Yellow
if (Test-Path "BuhoDesk-Installer") {
    Remove-Item -Recurse -Force "BuhoDesk-Installer"
}
New-Item -ItemType Directory -Path "BuhoDesk-Installer\Server" -Force | Out-Null
New-Item -ItemType Directory -Path "BuhoDesk-Installer\Client" -Force | Out-Null

# Step 4: Copy server files
Write-Host "Step 4: Copying server files..." -ForegroundColor Yellow
Copy-Item "BuhoServer\bin\Release\net9.0-windows\*" "BuhoDesk-Installer\Server\" -Recurse -Force

# Step 5: Copy client files
Write-Host "Step 5: Copying client files..." -ForegroundColor Yellow
Copy-Item "BuhoClient\bin\Release\net9.0-windows\*" "BuhoDesk-Installer\Client\" -Recurse -Force

# Step 6: Copy documentation
Write-Host "Step 6: Copying documentation..." -ForegroundColor Yellow
Copy-Item "README.md" "BuhoDesk-Installer\README.txt" -Force

# Step 7: Build installer
Write-Host "Step 7: Building installer..." -ForegroundColor Yellow
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $innoSetupPath) {
    & $innoSetupPath "BuhoDesk-Setup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to build installer" -ForegroundColor Red
        Read-Host "Press Enter to continue"
        exit 1
    }
} else {
    Write-Host "Error: Inno Setup not found at $innoSetupPath" -ForegroundColor Red
    Write-Host "Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    Read-Host "Press Enter to continue"
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Installer built successfully!" -ForegroundColor Green
Write-Host "Location: Output\BuhoDesk-Setup-v1.0.0.exe" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to continue"
