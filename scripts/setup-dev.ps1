#requires -Version 5.1
<#
.SYNOPSIS
    One-shot local dev bootstrap: brings up containers, seeds the SQS queue.

.DESCRIPTION
    Runs `docker compose up -d --build`, waits for LocalStack to be healthy,
    then creates the SQS queue. Run this once after cloning the repo.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    if (-not (Test-Path .env)) {
        Write-Host "Creating .env from .env.example..." -ForegroundColor Cyan
        Copy-Item .env.example .env
    }

    Write-Host "Starting containers..." -ForegroundColor Cyan
    docker compose up -d --build

    Write-Host "Waiting for LocalStack to be healthy..." -ForegroundColor Cyan
    $maxWait = 60
    $waited  = 0
    while ($waited -lt $maxWait) {
        $health = docker inspect --format '{{.State.Health.Status}}' toolbox-localstack 2>$null
        if ($health -eq "healthy") { break }
        Start-Sleep -Seconds 2
        $waited += 2
    }
    if ($health -ne "healthy") {
        throw "LocalStack did not become healthy within ${maxWait}s. Check 'docker compose logs localstack'."
    }

    & "$PSScriptRoot/setup-localstack.ps1"

    Write-Host ""
    Write-Host "All set." -ForegroundColor Green
    Write-Host "  UI:      http://localhost:5173" -ForegroundColor White
    Write-Host "  API:     http://localhost:8080/swagger" -ForegroundColor White
    Write-Host "  Seq:     http://localhost:5341" -ForegroundColor White
    Write-Host ""
    Write-Host "Next: cd polling-service/src/ToolboxManager.PollingService && dotnet run" -ForegroundColor Yellow
} finally {
    Pop-Location
}
