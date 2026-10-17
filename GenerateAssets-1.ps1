# PowerShell script to generate valid UWP Game Bar widget assets on Windows
Add-Type -AssemblyName System.Drawing

$assets = @{
    "Square150x150Logo.png" = @(150, 150)
    "Square150x150Logo.scale-100.png" = @(150, 150)
    "Square44x44Logo.png" = @(44, 44)
    "Square44x44Logo.scale-100.png" = @(44, 44)
    "Wide310x150Logo.png" = @(310, 150)
    "Wide310x150Logo.scale-100.png" = @(310, 150)
    "StoreLogo.png" = @(50, 50)
    "StoreLogo.scale-100.png" = @(50, 50)
    "SplashScreen.png" = @(620, 300)
    "SplashScreen.scale-100.png" = @(620, 300)
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetDir = Join-Path $scriptDir "GameBarWidget\Assets"
if (!(Test-Path $targetDir)) { 
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null 
}

$bgColor = [System.Drawing.Color]::FromArgb(19, 27, 38)
$accentColor = [System.Drawing.Color]::FromArgb(34, 197, 94)

foreach ($item in $assets.GetEnumerator()) {
    $w = $item.Value[0]
    $h = $item.Value[1]
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($bgColor)
    
    $borderWidth = [Math]::Max(2, [int]($w / 50))
    $pen = New-Object System.Drawing.Pen($accentColor, $borderWidth)
    $g.DrawRectangle($pen, 1, 1, ($w - 3), ($h - 3))
    
    $outPath = Join-Path $targetDir $item.Key
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "[OK] Created $outPath ($w x $h)" -ForegroundColor Green
}

Write-Host "All assets generated successfully with exact required pixel dimensions!" -ForegroundColor Cyan
