# Run API Service
Write-Host "Starting Order Processing API..." -ForegroundColor Green
Write-Host "Make sure connection strings are configured in appsettings.json" -ForegroundColor Yellow
Write-Host ""

cd $PSScriptRoot\src\OrderProcessingSystem.Api
dotnet run

