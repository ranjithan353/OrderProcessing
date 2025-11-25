# Quick Start Guide

## Prerequisites

- .NET 8 SDK installed
- Azure Subscription
- Azure CLI configured

## Local Development

### 1. Clone and Restore

```bash
cd OrderProcessingSystem
dotnet restore
dotnet build
```

### 2. Configure Settings

#### API (`src/OrderProcessingSystem.Api/appsettings.json`)

Update with your Azure service connection strings:

```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://...;AccountKey=...",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  },
  "EventGrid": {
    "TopicEndpoint": "https://order-events-topic.eastus-1.eventgrid.azure.net/api/events",
    "TopicKey": "your-key-here"
  }
}
```

#### Worker (`src/OrderProcessingSystem.Worker/appsettings.json`)

```json
{
  "EventHub": {
    "ConnectionString": "Endpoint=sb://...",
    "Name": "order-events-hub",
    "ConsumerGroup": "$Default"
  },
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "eventhub-checkpoints"
  },
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://...;AccountKey=...",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  }
}
```

### 3. Run Services

**Terminal 1 - API:**
```bash
cd src/OrderProcessingSystem.Api
dotnet run
```

API will be available at: `https://localhost:7000` or `http://localhost:5000`

**Terminal 2 - Worker:**
```bash
cd src/OrderProcessingSystem.Worker
dotnet run
```

### 4. Run Frontend

Open `frontend/index.html` in a browser, or use a local server:

```bash
cd frontend
python -m http.server 3000
```

Then open: `http://localhost:3000`

**Update API URL in `frontend/js/app.js`:**
```javascript
const API_BASE_URL = 'https://localhost:7000/api';
```

## Testing

### Create Order via API

```bash
curl -X POST https://localhost:7000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "John Doe",
    "customerEmail": "john@example.com",
    "shippingAddress": "123 Main St",
    "items": [
      {
        "productId": "PROD-001",
        "productName": "Laptop",
        "quantity": 1,
        "unitPrice": 999.99
      }
    ]
  }'
```

### Get All Orders

```bash
curl https://localhost:7000/api/orders
```

### Get Order by ID

```bash
curl https://localhost:7000/api/orders/{order-id}
```

## Azure Deployment

See `docs/AZURE_SETUP_STEPS.md` for detailed Azure setup instructions.

Quick summary:
1. Create resource group
2. Create Cosmos DB, Event Grid, Event Hub, Blob Storage
3. Create App Service plans
4. Deploy API and Worker
5. Configure APIM

## Project Structure

```
OrderProcessingSystem/
├── src/
│   ├── OrderProcessingSystem.Api/      # Web API
│   ├── OrderProcessingSystem.Worker/   # Worker Service
│   └── OrderProcessingSystem.Models/   # Shared Models
├── frontend/                           # Bootstrap UI
├── docs/                               # Documentation
└── Postman_Collection.json            # API Testing
```

## Troubleshooting

### API Not Starting
- Check connection strings in `appsettings.json`
- Verify .NET 8 SDK is installed
- Check port availability

### Events Not Processing
- Verify Event Grid subscription is active
- Check Event Hub connection string
- Verify Worker is running
- Check Blob Storage connection

### Frontend Not Connecting
- Update API URL in `app.js`
- Check CORS settings in API
- Verify API is running

## Next Steps

1. Review `README.md` for full documentation
2. Check `docs/ARCHITECTURE.md` for system design
3. Follow `docs/DEPLOYMENT.md` for Azure deployment
4. Import `Postman_Collection.json` for API testing

