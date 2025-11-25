# Run Both Services
Write-Host "Starting Order Processing System..." -ForegroundColor Green
Write-Host "This will open two PowerShell windows - one for API, one for Worker" -ForegroundColor Yellow
Write-Host ""

# Start API in new window
Start-Process powershell -ArgumentList "-NoExit", "-File", "$PSScriptRoot\run-api.ps1"

# Wait a moment
Start-Sleep -Seconds 2

# Start Worker in new window
Start-Process powershell -ArgumentList "-NoExit", "-File", "$PSScriptRoot\run-worker.ps1"

Write-Host "Both services are starting in separate windows..." -ForegroundColor Green
Write-Host "API will be available at: https://localhost:7000" -ForegroundColor Cyan
Write-Host "Swagger UI: https://localhost:7000/swagger" -ForegroundColor Cyan

