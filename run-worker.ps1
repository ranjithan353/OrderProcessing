# Run Worker Service
Write-Host "Starting Order Processing Worker..." -ForegroundColor Green
Write-Host "Make sure connection strings are configured in appsettings.json" -ForegroundColor Yellow
Write-Host ""

cd $PSScriptRoot\src\OrderProcessingSystem.Worker
dotnet run

