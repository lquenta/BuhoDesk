# BuhoDesk Cleanup Script
Write-Host "Cleaning up BuhoDesk project..." -ForegroundColor Green
Write-Host ""

# Step 1: Clean .NET build artifacts
Write-Host "Step 1: Cleaning .NET build artifacts..." -ForegroundColor Yellow
dotnet clean
if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: Some projects may not have been cleaned properly" -ForegroundColor Yellow
}

# Step 2: Remove build directories
Write-Host "Step 2: Removing build directories..." -ForegroundColor Yellow
$buildDirs = @(
    "BuhoServer\bin",
    "BuhoServer\obj", 
    "BuhoClient\bin",
    "BuhoClient\obj",
    "BuhoShared\bin",
    "BuhoShared\obj"
)

foreach ($dir in $buildDirs) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir
        Write-Host "  Removed: $dir" -ForegroundColor Cyan
    }
}

# Step 3: Remove installer build artifacts
Write-Host "Step 3: Removing installer build artifacts..." -ForegroundColor Yellow
if (Test-Path "BuhoDesk-Installer") {
    Remove-Item -Recurse -Force "BuhoDesk-Installer"
    Write-Host "  Removed: BuhoDesk-Installer/" -ForegroundColor Cyan
}

# Step 4: Remove log files
Write-Host "Step 4: Removing log files..." -ForegroundColor Yellow
if (Test-Path "Logs") {
    Remove-Item -Recurse -Force "Logs"
    Write-Host "  Removed: Logs/" -ForegroundColor Cyan
}

# Step 5: Remove temporary files
Write-Host "Step 5: Removing temporary files..." -ForegroundColor Yellow
Get-ChildItem -Recurse -File | Where-Object { 
    $_.Name -match "\.(tmp|temp|log|pdb|cache)$" -or 
    $_.Name -match "~$" -or
    $_.Name -match "\.swp$"
} | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "  Removed: $($_.Name)" -ForegroundColor Cyan
}

# Step 6: Remove IDE-specific files
Write-Host "Step 6: Removing IDE-specific files..." -ForegroundColor Yellow
$ideDirs = @(".vs", ".vscode", ".idea")
foreach ($dir in $ideDirs) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir
        Write-Host "  Removed: $dir/" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Cleanup completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "To rebuild the project, run:" -ForegroundColor Cyan
Write-Host "  dotnet build" -ForegroundColor White
Write-Host ""
Write-Host "To build the installer, run:" -ForegroundColor Cyan
Write-Host "  .\build-installer.ps1" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter to continue"
