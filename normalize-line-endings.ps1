# Normalize Line Endings to CRLF (Windows)
# Processes only source files, skips bin/obj folders

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Normalizing Line Endings to CRLF" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$count = 0
$extensions = @("*.cs", "*.xaml", "*.md", "*.ps1", "*.json", "*.xml", "*.sln", "*.csproj", "*.gitignore", "*.gitattributes")

foreach ($ext in $extensions) {
    Write-Host "Processing $ext files..." -ForegroundColor Yellow
    
    Get-ChildItem -Path . -Filter $ext -Recurse -File | 
        Where-Object { $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' } |
        ForEach-Object {
            try {
                $content = Get-Content $_.FullName -Raw
                if ($content) {
                    # Normalize to LF first, then to CRLF to ensure consistency
                    $normalized = $content -replace "`r`n", "`n" -replace "`r", "`n" -replace "`n", "`r`n"
                    [System.IO.File]::WriteAllText($_.FullName, $normalized, [System.Text.UTF8Encoding]::new($false))
                    $count++
                    Write-Host "  ? $($_.Name)" -ForegroundColor Green
                }
            }
            catch {
                Write-Host "  ? Failed: $($_.Name) - $($_.Exception.Message)" -ForegroundColor Red
            }
        }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Normalized $count files to CRLF" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review changes in Git" -ForegroundColor White
Write-Host "  2. Commit: 'Normalize line endings to CRLF'" -ForegroundColor White
Write-Host "  3. Push to GitHub" -ForegroundColor White
Write-Host ""
