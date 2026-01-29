# Remove Large Files from Git Tracking
# This removes the runtime installer from Git without deleting the local file

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Removing Large Files from Git Tracking" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$runtimeInstaller = "FireStickScreenSaverEnforcer.App\windowsappruntimeinstall-x64.exe"

# Check if file exists
if (Test-Path $runtimeInstaller) {
    $size = [Math]::Round((Get-Item $runtimeInstaller).Length / 1MB, 2)
    Write-Host "Found: $runtimeInstaller ($size MB)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "This file is too large for GitHub (limit: 100 MB)" -ForegroundColor Yellow
    Write-Host "Removing from Git tracking (file will remain on your computer)..." -ForegroundColor Yellow
    Write-Host ""
    
    # Remove from Git cache
    git rm --cached $runtimeInstaller 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Removed from Git tracking" -ForegroundColor Green
    } else {
        Write-Host "? File was not in Git tracking" -ForegroundColor Cyan
    }
} else {
    Write-Host "? Runtime installer not found (that's okay)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Checking for other large files..." -ForegroundColor Yellow

# Check for any files over 50 MB in the staging area
$largeFiles = git ls-files -z | ForEach-Object {
    $file = $_ -replace '\0', ''
    if (Test-Path $file) {
        $size = (Get-Item $file).Length
        if ($size -gt 50MB) {
            [PSCustomObject]@{
                File = $file
                SizeMB = [Math]::Round($size / 1MB, 2)
            }
        }
    }
} 2>$null

if ($largeFiles) {
    Write-Host ""
    Write-Host "? Other large files found in Git:" -ForegroundColor Yellow
    $largeFiles | Format-Table File, SizeMB -AutoSize
    Write-Host ""
    Write-Host "Consider adding them to .gitignore if they're build artifacts." -ForegroundColor Yellow
} else {
    Write-Host "? No other large files found" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps in GitHub Desktop:" -ForegroundColor Yellow
Write-Host "  1. You should see .gitignore as modified" -ForegroundColor White
Write-Host "  2. The runtime installer should NOT appear" -ForegroundColor White
Write-Host "  3. Commit message: 'Ignore runtime installer (too large for GitHub)'" -ForegroundColor White
Write-Host "  4. Commit and push" -ForegroundColor White
Write-Host ""
Write-Host "Important:" -ForegroundColor Yellow
Write-Host "  • The runtime installer stays on your computer" -ForegroundColor White
Write-Host "  • It will be included in builds (via .csproj)" -ForegroundColor White
Write-Host "  • It will be in the ZIP you upload to GitHub Releases" -ForegroundColor White
Write-Host "  • It just won't be in the Git repository" -ForegroundColor White
Write-Host ""
