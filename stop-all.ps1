# Stops the service windows launched by start-all.ps1, using the PIDs it
# recorded in .start-all-pids.txt. Each recorded PID is the powershell.exe
# window itself; taskkill /T kills that window's whole process tree so the
# dotnet build/run child process(es) underneath it die too, not just the
# window.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$pidFile = Join-Path $root ".start-all-pids.txt"

if (-not (Test-Path $pidFile)) {
    Write-Warning "No record of services started by start-all.ps1 (missing $pidFile). Nothing to stop."
    return
}

$lines = Get-Content -LiteralPath $pidFile | Where-Object { $_.Trim() -ne "" }

if ($lines.Count -eq 0) {
    Write-Warning "PID file is empty. Nothing to stop."
    Remove-Item -LiteralPath $pidFile -Force
    return
}

foreach ($line in $lines) {
    $name, $procIdText = $line -split ",", 2
    $procId = 0
    if (-not [int]::TryParse($procIdText, [ref]$procId)) {
        Write-Warning "Skipping malformed entry: $line"
        continue
    }

    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if (-not $proc) {
        Write-Host "$name (PID $procId) already stopped."
        continue
    }

    Write-Host "Stopping $name (PID $procId) and its child processes..."
    try {
        taskkill /PID $procId /T /F | Out-Null
    }
    catch {
        Write-Warning "Failed to stop $name (PID $procId): $_"
    }
}

Remove-Item -LiteralPath $pidFile -Force
Write-Host "All tracked service windows stopped."
