# Quick Commit & Push Script
# Run this to commit all changes and push to GitHub

Write-Host "Git Commit & Push Helper" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Check if git is available
try {
    git --version | Out-Null
} catch {
    Write-Host "? Git is not installed or not in PATH!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please use Visual Studio instead:" -ForegroundColor Yellow
    Write-Host "  1. View ? Git Changes (Ctrl+0, Ctrl+G)" -ForegroundColor White
    Write-Host "  2. Stage all changes" -ForegroundColor White
    Write-Host "  3. Enter commit message" -ForegroundColor White
    Write-Host "  4. Click 'Commit All and Push'" -ForegroundColor White
    exit 1
}

# Show status
Write-Host "Current Status:" -ForegroundColor Yellow
git status --short
Write-Host ""

# Confirm
$confirm = Read-Host "Commit and push all changes? (y/n)"
if ($confirm -ne 'y') {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

# Stage all
Write-Host ""
Write-Host "Staging changes..." -ForegroundColor Yellow
git add .

# Commit
Write-Host "Enter commit message:" -ForegroundColor Yellow
$message = Read-Host
if ([string]::IsNullOrWhiteSpace($message)) {
    $message = "Update: Fire TV Screensaver Enforcer improvements"
}

git commit -m $message

# Push
Write-Host ""
Write-Host "Pushing to GitHub..." -ForegroundColor Yellow
git push origin master

Write-Host ""
Write-Host "? Done! Check your repo:" -ForegroundColor Green
Write-Host "  https://github.com/Rolling2405/FireStickScreenSaverEnforcer" -ForegroundColor Cyan
Write-Host ""
