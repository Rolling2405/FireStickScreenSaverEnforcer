# Fire TV Screensaver Timeout Enforcer - Build & Publish Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fire TV Screensaver Timeout Enforcer" -ForegroundColor Cyan
Write-Host "Build & Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$projectPath = "FireStickScreenSaverEnforcer.App"
$version = "v1.5.0"
$outputZip = "FireStickScreenSaverEnforcer-$version-win-x64.zip"
$publishPath = "$projectPath\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"

# Step 1: Clean previous builds
Write-Host "[1/5] Cleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path "$projectPath\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$projectPath\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $outputZip -Force -ErrorAction SilentlyContinue
Write-Host "  ? Clean complete" -ForegroundColor Green
Write-Host ""

# Step 2: Restore dependencies
Write-Host "[2/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? Restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ? Restore complete" -ForegroundColor Green
Write-Host ""

# Step 3: Build release
Write-Host "[3/5] Building Release (self-contained)..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ? Build complete" -ForegroundColor Green
Write-Host ""

# Step 4: Verify and strip publish output
Write-Host "[4/5] Verifying and stripping publish output..." -ForegroundColor Yellow

if (-not (Test-Path $publishPath)) {
    Write-Host "  ? Publish folder not found: $publishPath" -ForegroundColor Red
    exit 1
}

# Check ADB tools
if (Test-Path "$publishPath\platform-tools\adb.exe") {
    Write-Host "  ? ADB tools included" -ForegroundColor Green
} else {
    Write-Host "  ? ADB tools not found (will cause errors at runtime)" -ForegroundColor Yellow
}

# Check app executable
if (Test-Path "$publishPath\FireStickScreenSaverEnforcer.exe") {
    Write-Host "  ? Application executable present" -ForegroundColor Green
} else {
    Write-Host "  ? Application executable not found!" -ForegroundColor Red
    exit 1
}

# Remove unnecessary files not needed at runtime
$filesToRemove = @(
    "windowsappruntimeinstall-x64.exe",
    "onnxruntime.dll",
    "DirectML.dll",
    "Microsoft.DiaSymReader.Native.amd64.dll",
    "mscordaccore.dll",
    "mscordbi.dll"
)
Get-ChildItem -Path $publishPath -Filter "mscordaccore_*.dll" | Remove-Item -Force
foreach ($file in $filesToRemove) {
    $filePath = Join-Path $publishPath $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "  Removed: $file" -ForegroundColor DarkGray
    }
}
Write-Host "  ? Strip complete" -ForegroundColor Green
Write-Host ""

# Step 5: Create ZIP package
Write-Host "[5/5] Creating distribution ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "$publishPath\*" -DestinationPath $outputZip -Force
Write-Host "  ? ZIP created: $outputZip" -ForegroundColor Green
Write-Host ""

# Summary
$zipSize = [Math]::Round((Get-Item $outputZip).Length / 1MB, 2)
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release package : $outputZip" -ForegroundColor White
Write-Host "Size            : $zipSize MB" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Test the app from: $publishPath" -ForegroundColor White
Write-Host "  2. Push to GitHub" -ForegroundColor White
Write-Host "  3. Upload $outputZip to the GitHub release" -ForegroundColor White
Write-Host ""
