# PowerShell script to create a simple owl icon for BuhoDesk
Add-Type -AssemblyName System.Drawing

# Create a new bitmap for the icon (32x32 pixels)
$bitmap = New-Object System.Drawing.Bitmap(32, 32)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Set background to transparent
$graphics.Clear([System.Drawing.Color]::Transparent)

# Define colors
$brown = [System.Drawing.Color]::FromArgb(139, 69, 19)
$lightBrown = [System.Drawing.Color]::FromArgb(160, 82, 45)
$yellow = [System.Drawing.Color]::FromArgb(255, 215, 0)
$black = [System.Drawing.Color]::Black

# Create brushes
$brownBrush = New-Object System.Drawing.SolidBrush($brown)
$lightBrownBrush = New-Object System.Drawing.SolidBrush($lightBrown)
$yellowBrush = New-Object System.Drawing.SolidBrush($yellow)
$blackBrush = New-Object System.Drawing.SolidBrush($black)

try {
    # Draw owl body
    $graphics.FillEllipse($brownBrush, 8, 12, 16, 16)
    
    # Draw owl head
    $graphics.FillEllipse($lightBrownBrush, 10, 6, 12, 12)
    
    # Draw eyes
    $graphics.FillEllipse($yellowBrush, 12, 8, 4, 4)
    $graphics.FillEllipse($yellowBrush, 16, 8, 4, 4)
    
    # Draw pupils
    $graphics.FillEllipse($blackBrush, 13, 9, 2, 2)
    $graphics.FillEllipse($blackBrush, 17, 9, 2, 2)
    
    # Draw beak
    $beakPoints = @(
        [System.Drawing.Point]::new(15, 12),
        [System.Drawing.Point]::new(14, 14),
        [System.Drawing.Point]::new(16, 14)
    )
    $graphics.FillPolygon($yellowBrush, $beakPoints)
    
    # Draw wings
    $graphics.FillEllipse($lightBrownBrush, 6, 16, 4, 6)
    $graphics.FillEllipse($lightBrownBrush, 22, 16, 4, 6)
    
    # Draw feet
    $graphics.FillRectangle($blackBrush, 13, 26, 2, 3)
    $graphics.FillRectangle($blackBrush, 17, 26, 2, 3)
    
    # Save as PNG
    $bitmap.Save("owl-icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
    
    Write-Host "✅ Owl icon created successfully!" -ForegroundColor Green
    
} finally {
    $brownBrush.Dispose()
    $lightBrownBrush.Dispose()
    $yellowBrush.Dispose()
    $blackBrush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "🦉 BuhoDesk Owl Icon created!" -ForegroundColor Yellow
