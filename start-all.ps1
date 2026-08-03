# Launches every service in its own PowerShell window (most via `dotnet run`, QStash via its
# local dev-server CLI), and records each window's PID in .start-all-pids.txt so stop-all.ps1
# can find and close them again without you having to close windows by hand.
# Ports: QStash dev server 8080, RecipeService 5081, PantryService 5082,
# SubstitutionService 5083, SourcingService 5084, Gateway (GraphQL) 5085.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$pidFile = Join-Path $root ".start-all-pids.txt"

$services = @(
    @{ Name = "QStash";              Path = $root;                                                    Command = "npx @upstash/qstash-cli@latest dev" },
    @{ Name = "RecipeService";       Path = Join-Path $root "RecipeService\RecipeService";             Command = "dotnet run" },
    @{ Name = "PantryService";       Path = Join-Path $root "PantryService\PantryService";             Command = "dotnet run" },
    @{ Name = "SubstitutionService"; Path = Join-Path $root "SubstitutionService\SubstitutionService"; Command = "dotnet run" },
    @{ Name = "SourcingService";     Path = Join-Path $root "SourcingService\SourcingService";         Command = "dotnet run" },
    @{ Name = "Gateway";             Path = Join-Path $root "Gateway\Gateway";                         Command = "dotnet run" }
)

$launched = @()

foreach ($service in $services) {
    if (-not (Test-Path $service.Path)) {
        Write-Warning "Skipping $($service.Name): path not found at $($service.Path)"
        continue
    }

    Write-Host "Starting $($service.Name)..."
    $proc = Start-Process powershell.exe -PassThru -ArgumentList @(
        "-NoExit",
        "-Command",
        "`$Host.UI.RawUI.WindowTitle = '$($service.Name)'; Set-Location -LiteralPath '$($service.Path)'; $($service.Command)"
    )

    $launched += "$($service.Name),$($proc.Id)"
}

Set-Content -LiteralPath $pidFile -Value $launched -Encoding utf8

Write-Host "All services launching:"
Write-Host "  QStash (local dev)  http://127.0.0.1:8080"
Write-Host "  RecipeService       http://localhost:5081"
Write-Host "  PantryService       http://localhost:5082"
Write-Host "  SubstitutionService http://localhost:5083"
Write-Host "  SourcingService     http://localhost:5084"
Write-Host "  Gateway (GraphQL)   http://localhost:5085/graphql"
Write-Host ""
Write-Host "Run .\stop-all.ps1 to close all of these windows."
