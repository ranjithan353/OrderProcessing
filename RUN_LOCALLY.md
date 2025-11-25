# How to Run the Solution Locally

## Prerequisites

- .NET 8 SDK installed
- Azure services configured (Cosmos DB, Event Grid, Event Hub, Blob Storage)
- Connection strings ready

## Step 1: Configure Connection Strings

### API Configuration

Edit: `src/OrderProcessingSystem.Api/appsettings.json`

```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://YOUR-COSMOS-ACCOUNT.documents.azure.com:443/;AccountKey=YOUR-KEY",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  },
  "EventGrid": {
    "TopicEndpoint": "https://YOUR-TOPIC.eastus-1.eventgrid.azure.net/api/events",
    "TopicKey": "YOUR-EVENT-GRID-KEY"
  }
}
```

### Worker Configuration

Edit: `src/OrderProcessingSystem.Worker/appsettings.json`

```json
{
  "EventHub": {
    "ConnectionString": "Endpoint=sb://YOUR-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR-KEY",
    "Name": "order-events-hub",
    "ConsumerGroup": "$Default"
  },
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=YOUR-ACCOUNT;AccountKey=YOUR-KEY;EndpointSuffix=core.windows.net",
    "ContainerName": "eventhub-checkpoints"
  },
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://YOUR-COSMOS-ACCOUNT.documents.azure.com:443/;AccountKey=YOUR-KEY",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  }
}
```

## Step 2: Build the Solution

```powershell
cd D:\OrderProcessingSystem
dotnet restore
dotnet build
```

## Step 3: Run the Services

You need to run **TWO** services simultaneously:

### Option A: Two Terminal Windows (Recommended)

**Terminal 1 - API:**
```powershell
cd D:\OrderProcessingSystem\src\OrderProcessingSystem.Api
dotnet run
```

**Terminal 2 - Worker:**
```powershell
cd D:\OrderProcessingSystem\src\OrderProcessingSystem.Worker
dotnet run
```

### Option B: Single Terminal (Background Process)

**Run API in background:**
```powershell
cd D:\OrderProcessingSystem\src\OrderProcessingSystem.Api
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run"
```

**Then run Worker:**
```powershell
cd D:\OrderProcessingSystem\src\OrderProcessingSystem.Worker
dotnet run
```

## Step 4: Verify Services Are Running

### API Service
- Should show: `Now listening on: https://localhost:7000` or `http://localhost:5000`
- Swagger UI: `https://localhost:7000/swagger` or `http://localhost:5000/swagger`

### Worker Service
- Should show: `Event Hub Processor Service is starting.`
- Should show: `Event Hub Processor started successfully.`

## Step 5: Test the API

### Using Browser
1. Open: `https://localhost:7000/swagger`
2. Test the endpoints through Swagger UI

### Using PowerShell/curl
```powershell
# Get all orders
Invoke-RestMethod -Uri "https://localhost:7000/api/orders" -Method Get

# Create an order
$body = @{
    customerName = "Test User"
    customerEmail = "test@example.com"
    shippingAddress = "123 Test St"
    items = @(
        @{
            productId = "PROD-001"
            productName = "Test Product"
            quantity = 1
            unitPrice = 10.00
        }
    )
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7000/api/orders" -Method Post -Body $body -ContentType "application/json"
```

## Step 6: Run Frontend

### Option A: Simple HTTP Server
```powershell
cd D:\OrderProcessingSystem\frontend
python -m http.server 3000
```

Then open: `http://localhost:3000`

### Option B: VS Code Live Server
1. Install "Live Server" extension
2. Right-click on `index.html`
3. Select "Open with Live Server"

### Option C: Direct File Open
- Just open `frontend/index.html` in browser
- **Note:** Update API URL in `app.js` if needed

## Troubleshooting

### Port Already in Use
If port 7000 or 5000 is in use:
1. Check what's using it: `netstat -ano | findstr :7000`
2. Kill the process or change port in `launchSettings.json`

### Connection String Errors
- Verify connection strings are correct
- Check Azure services are running
- Ensure database/container exists

### CORS Errors
- API allows `localhost:3000` by default
- If using different port, update CORS in `Program.cs`

### Worker Not Processing Events
- Verify Event Grid subscription is active
- Check Event Hub connection string
- Verify Blob Storage container exists
- Check Worker logs for errors

## Expected Output

### API Console:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Worker Console:
```
info: OrderProcessingSystem.Worker.Services.EventHubProcessorService[0]
      Event Hub Processor Service is starting.
info: OrderProcessingSystem.Worker.Services.EventHubProcessorService[0]
      Event Hub Processor started successfully.
```

## Stopping Services

Press `Ctrl+C` in each terminal to stop the services.

## Next Steps

1. Test creating orders via API
2. Verify orders appear in Cosmos DB
3. Check events are processed by Worker
4. Verify order status updates to "Processed"

