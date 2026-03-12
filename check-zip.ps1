Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $PSScriptRoot 'FireStickScreenSaverEnforcer-v1.5.0-win-x64.zip'
$z = [System.IO.Compression.ZipFile]::OpenRead($zipPath)

$entries = @()
foreach ($e in $z.Entries) {
    $entries += [PSCustomObject]@{
        Name = $e.FullName
        MB   = [Math]::Round($e.Length / 1MB, 2)
    }
}
$z.Dispose()

Write-Host "=== Top 20 largest files ===" -ForegroundColor Cyan
$sorted = $entries | Sort-Object MB -Descending
$sorted | Select-Object -First 20 | Format-Table -AutoSize

Write-Host ""
$count = $entries.Count
$totalMB = [Math]::Round(($entries | Measure-Object -Property MB -Sum).Sum, 2)
Write-Host "=== Total file count: $count" -ForegroundColor Yellow
Write-Host "=== Total uncompressed size: $totalMB MB" -ForegroundColor Yellow

Write-Host ""
Write-Host "=== Suspicious / unexpected large items (>1 MB) ===" -ForegroundColor Red
$sorted | Where-Object { $_.MB -gt 1 } | Format-Table -AutoSize
