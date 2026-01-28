# Fire TV Screensaver Timeout Enforcer - Enhanced Build & Publish Script
# Includes automatic Windows App SDK Runtime installer download

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fire TV Screensaver Timeout Enforcer" -ForegroundColor Cyan
Write-Host "Build & Publish Script v2.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$projectPath = "FireStickScreenSaverEnforcer.App"
$version = "v1.0.0"
$outputZip = "FireStickScreenSaverEnforcer-$version-win-x64.zip"
$runtimeInstallerUrl = "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe"
$runtimeInstaller = "$projectPath\windowsappruntimeinstall-x64.exe"

# Step 1: Check/Download Runtime Installer
Write-Host "[1/6] Checking Windows App SDK Runtime installer..." -ForegroundColor Yellow
if (Test-Path $runtimeInstaller) {
    $size = [Math]::Round((Get-Item $runtimeInstaller).Length / 1MB, 2)
    Write-Host "  ? Runtime installer found ($size MB)" -ForegroundColor Green
} else {
    Write-Host "  Downloading runtime installer..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $runtimeInstallerUrl -OutFile $runtimeInstaller -UseBasicParsing
        $size = [Math]::Round((Get-Item $runtimeInstaller).Length / 1MB, 2)
        Write-Host "  ? Downloaded runtime installer ($size MB)" -ForegroundColor Green
    } catch {
        Write-Host "  ? Failed to download runtime installer" -ForegroundColor Red
        Write-Host "  Please download manually from: $runtimeInstallerUrl" -ForegroundColor Yellow
        exit 1
    }
}
Write-Host ""

# Step 2: Clean previous builds
Write-Host "[2/6] Cleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path "$projectPath\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$projectPath\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $outputZip -Force -ErrorAction SilentlyContinue
Write-Host "  ? Clean complete" -ForegroundColor Green
Write-Host ""

# Step 3: Restore dependencies
Write-Host "[3/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? Restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ? Restore complete" -ForegroundColor Green
Write-Host ""

# Step 4: Build release
Write-Host "[4/6] Building Release (self-contained with bundled runtime)..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  ? Build complete" -ForegroundColor Green
Write-Host ""

# Step 5: Verify bundled files
Write-Host "[5/6] Verifying bundled files..." -ForegroundColor Yellow
$publishPath = "$projectPath\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"

if (-not (Test-Path $publishPath)) {
    Write-Host "  ? Publish folder not found: $publishPath" -ForegroundColor Red
    exit 1
}

# Check runtime installer
if (Test-Path "$publishPath\windowsappruntimeinstall-x64.exe") {
    $size = [Math]::Round((Get-Item "$publishPath\windowsappruntimeinstall-x64.exe").Length / 1MB, 2)
    Write-Host "  ? Runtime installer: $size MB" -ForegroundColor Green
} else {
    Write-Host "  ? Runtime installer not found in publish folder!" -ForegroundColor Red
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

Write-Host ""

# Step 6: Create ZIP package
Write-Host "[6/6] Creating distribution ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "$publishPath\*" -DestinationPath $outputZip -Force
Write-Host "  ? ZIP created: $outputZip" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$zipSize = [Math]::Round((Get-Item $outputZip).Length / 1MB, 2)
Write-Host "Release package: $outputZip" -ForegroundColor White
Write-Host "Size: $zipSize MB" -ForegroundColor White
Write-Host ""
Write-Host "Package contents:" -ForegroundColor Yellow
Write-Host "  • .NET 10 Runtime (self-contained)" -ForegroundColor White
Write-Host "  • Windows App SDK Runtime installer (auto-install on first launch)" -ForegroundColor White
Write-Host "  • ADB tools (platform-tools)" -ForegroundColor White
Write-Host "  • Your application" -ForegroundColor White
Write-Host ""
Write-Host "User experience:" -ForegroundColor Yellow
Write-Host "  1. Extract ZIP" -ForegroundColor White
Write-Host "  2. Run FireStickScreenSaverEnforcer.exe" -ForegroundColor White
Write-Host "  3. Click 'Install' when prompted (one-time)" -ForegroundColor White
Write-Host "  4. App restarts and works!" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Test the app from: $publishPath" -ForegroundColor White
Write-Host "  2. Commit changes (excluding the ZIP)" -ForegroundColor White
Write-Host "  3. Push to GitHub" -ForegroundColor White
Write-Host "  4. Update release and upload $outputZip" -ForegroundColor White
Write-Host ""
