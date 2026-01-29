# Fix Large File Push Issue
# This removes the release ZIP from Git (it should only be uploaded to GitHub Releases)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fix: Remove Large Files from Git" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if ZIP file is tracked
$zipFile = "FireStickScreenSaverEnforcer-v1.0.0-win-x64.zip"

if (Test-Path $zipFile) {
    Write-Host "Found ZIP file: $zipFile" -ForegroundColor Yellow
    $size = [Math]::Round((Get-Item $zipFile).Length / 1MB, 2)
    Write-Host "Size: $size MB" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Removing ZIP files from Git cache..." -ForegroundColor Yellow
git rm --cached *.zip 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ? Removed from Git cache" -ForegroundColor Green
} else {
    Write-Host "  ? No ZIP files in Git cache" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Removing large files from staging..." -ForegroundColor Yellow
git reset HEAD *.zip 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ? Unstaged" -ForegroundColor Green
} else {
    Write-Host "  ? Nothing to unstage" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Fixed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "What happened:" -ForegroundColor Yellow
Write-Host "  • ZIP files are now in .gitignore" -ForegroundColor White
Write-Host "  • Removed from Git tracking" -ForegroundColor White
Write-Host "  • File still exists locally (safe)" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. In GitHub Desktop or Visual Studio:" -ForegroundColor White
Write-Host "     - Commit the .gitignore change" -ForegroundColor White
Write-Host "     - Push to GitHub (should be fast now!)" -ForegroundColor White
Write-Host ""
Write-Host "  2. Upload ZIP to GitHub Release:" -ForegroundColor White
Write-Host "     - Go to: https://github.com/Rolling2405/FireStickScreenSaverEnforcer/releases/new" -ForegroundColor Cyan
Write-Host "     - Drag $zipFile to 'Attach binaries'" -ForegroundColor White
Write-Host ""
Write-Host "Remember: ZIP files should NEVER be committed to Git!" -ForegroundColor Red
Write-Host "They're too large and belong on the Releases page only." -ForegroundColor Red
Write-Host ""
