# Fire TV Screensaver Timeout Enforcer - Build & Publish Script
# Run this script to create a release build and package it for GitHub

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fire TV Screensaver Timeout Enforcer" -ForegroundColor Cyan
Write-Host "Build & Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$projectPath = "FireStickScreenSaverEnforcer.App"
$version = "v1.0.0"
$outputZip = "FireStickScreenSaverEnforcer-$version-win-x64.zip"

# Step 1: Clean previous builds
Write-Host "[1/4] Cleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path "$projectPath\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$projectPath\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $outputZip -Force -ErrorAction SilentlyContinue
Write-Host "  ? Clean complete" -ForegroundColor Green
Write-Host ""

# Step 2: Restore dependencies
Write-Host "[2/4] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? Restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ? Restore complete" -ForegroundColor Green
Write-Host ""

# Step 3: Build release
Write-Host "[3/4] Building Release (self-contained)..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ? Build complete" -ForegroundColor Green
Write-Host ""

# Step 4: Create ZIP package
Write-Host "[4/4] Creating distribution ZIP..." -ForegroundColor Yellow
$publishPath = "$projectPath\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"

if (-not (Test-Path $publishPath)) {
    Write-Host "  ? Publish folder not found: $publishPath" -ForegroundColor Red
    exit 1
}


Compress-Archive -Path "$publishPath\*" -DestinationPath $outputZip -Force
Write-Host "  ? ZIP created: $outputZip" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release package: $outputZip" -ForegroundColor White
Write-Host "Size: $([Math]::Round((Get-Item $outputZip).Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Test the app from: $publishPath" -ForegroundColor White
Write-Host "  2. Commit and push your code to GitHub" -ForegroundColor White
Write-Host "  3. Create a new release at:" -ForegroundColor White
Write-Host "     https://github.com/Rolling2405/FireStickScreenSaverEnforcer/releases/new" -ForegroundColor Cyan
Write-Host "  4. Upload $outputZip to the release" -ForegroundColor White
Write-Host ""
