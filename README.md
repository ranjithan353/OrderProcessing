# Order Processing System

A fully functional, event-driven Order Processing & Notification System built with .NET 8 and Azure cloud services.

## Architecture

```
┌─────────────┐
│   Frontend  │
│  (Bootstrap)│
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────┐
│      API Management (APIM)       │
│  - Subscription Key Required    │
│  - Rate Limiting (5 req/min)    │
│  - Logging & Transformation      │
└──────┬──────────────────────────┘
       │
       ▼
┌─────────────────────────────────┐
│      .NET 8 Web API             │
│  - POST /api/orders             │
│  - GET /api/orders/{id}         │
│  - GET /api/orders              │
└──────┬──────────────────────────┘
       │
       ├──────────────────┐
       ▼                  ▼
┌──────────────┐   ┌──────────────┐
│ Cosmos DB    │   │ Event Grid   │
│ (Orders)     │   │ (Topic)      │
└──────────────┘   └──────┬───────┘
                          │
                          ▼
                   ┌──────────────┐
                   │  Event Hub   │
                   │ (order-events)│
                   └──────┬───────┘
                          │
                          ▼
              ┌───────────────────────┐
              │ .NET 8 Worker Service │
              │ - Consume Events       │
              │ - Update Order Status  │
              └───────────────────────┘
```

## Components

### 1. Web API (`OrderProcessingSystem.Api`)
- RESTful API for order management
- Cosmos DB integration for data persistence
- Event Grid integration for event publishing
- Retry logic with Polly
- Comprehensive error handling

### 2. Worker Service (`OrderProcessingSystem.Worker`)
- Event Hub consumer
- Processes order events
- Updates order status in Cosmos DB
- Checkpoint management with Blob Storage

### 3. Frontend
- Modern Bootstrap 5 UI
- Responsive design
- Order creation and viewing
- Real-time order status updates

## Prerequisites

- .NET 8 SDK
- Azure Subscription
- Visual Studio 2022 or VS Code
- Azure CLI (for deployment)

## Azure Services Required

1. **Azure Cosmos DB** (SQL API)
2. **Azure Event Grid** (Custom Topic)
3. **Azure Event Hub**
4. **Azure Blob Storage** (for Event Hub checkpoints)
5. **Azure API Management** (APIM)
6. **Azure App Service** (for hosting)

## Local Development Setup

### 1. Clone and Build

```bash
cd OrderProcessingSystem
dotnet restore
dotnet build
```

### 2. Configure App Settings

#### API (`appsettings.json`)

```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://...;AccountKey=...",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  },
  "EventGrid": {
    "TopicEndpoint": "https://order-events-topic.eastus-1.eventgrid.azure.net/api/events",
    "TopicKey": "your-event-grid-key"
  }
}
```

#### Worker (`appsettings.json`)

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

### 3. Run Locally

```bash
# Terminal 1 - API
cd src/OrderProcessingSystem.Api
dotnet run

# Terminal 2 - Worker
cd src/OrderProcessingSystem.Worker
dotnet run
```

### 4. Run Frontend

Open `frontend/index.html` in a browser or use a local web server:

```bash
cd frontend
python -m http.server 3000
```

Update `frontend/js/app.js` with your API URL.

## Azure Deployment Steps

### Step 1: Create Resource Group

```bash
az group create --name order-processing-rg --location eastus
```

### Step 2: Create Cosmos DB

```bash
# Create Cosmos DB account
az cosmosdb create \
  --name order-processing-cosmos \
  --resource-group order-processing-rg \
  --default-consistency-level Session

# Create database
az cosmosdb sql database create \
  --account-name order-processing-cosmos \
  --resource-group order-processing-rg \
  --name OrderProcessingDB

# Create container
az cosmosdb sql container create \
  --account-name order-processing-cosmos \
  --resource-group order-processing-rg \
  --database-name OrderProcessingDB \
  --name Orders \
  --partition-key-path "/id" \
  --throughput 400

# Get connection string
az cosmosdb keys list \
  --name order-processing-cosmos \
  --resource-group order-processing-rg \
  --type connection-strings
```

### Step 3: Create Event Grid Topic

```bash
# Create Event Grid topic
az eventgrid topic create \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --location eastus

# Get topic endpoint and key
az eventgrid topic show \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --query "endpoint"

az eventgrid topic key list \
  --name order-events-topic \
  --resource-group order-processing-rg
```

### Step 4: Create Event Hub

```bash
# Create Event Hub namespace
az eventhubs namespace create \
  --name order-events-namespace \
  --resource-group order-processing-rg \
  --location eastus \
  --sku Standard

# Create Event Hub
az eventhubs eventhub create \
  --name order-events-hub \
  --namespace-name order-events-namespace \
  --resource-group order-processing-rg \
  --partition-count 2

# Get connection string
az eventhubs namespace authorization-rule keys list \
  --name RootManageSharedAccessKey \
  --namespace-name order-events-namespace \
  --resource-group order-processing-rg \
  --query primaryConnectionString
```

### Step 5: Create Blob Storage

```bash
# Create storage account
az storage account create \
  --name orderprocessingstorage \
  --resource-group order-processing-rg \
  --location eastus \
  --sku Standard_LRS

# Create container
az storage container create \
  --name eventhub-checkpoints \
  --account-name orderprocessingstorage

# Get connection string
az storage account show-connection-string \
  --name orderprocessingstorage \
  --resource-group order-processing-rg
```

### Step 6: Configure Event Grid → Event Hub Subscription

```bash
# Create Event Grid subscription
az eventgrid event-subscription create \
  --name order-events-subscription \
  --source-resource-id /subscriptions/{subscription-id}/resourceGroups/order-processing-rg/providers/Microsoft.EventGrid/topics/order-events-topic \
  --endpoint-type eventhub \
  --endpoint /subscriptions/{subscription-id}/resourceGroups/order-processing-rg/providers/Microsoft.EventHub/namespaces/order-events-namespace/eventhubs/order-events-hub
```

### Step 7: Create App Service Plans

```bash
# Create App Service Plan for API
az appservice plan create \
  --name order-api-plan \
  --resource-group order-processing-rg \
  --sku B1 \
  --is-linux

# Create App Service Plan for Worker
az appservice plan create \
  --name order-worker-plan \
  --resource-group order-processing-rg \
  --sku B1 \
  --is-linux
```

### Step 8: Deploy Web API

```bash
# Create Web App
az webapp create \
  --name order-processing-api \
  --resource-group order-processing-rg \
  --plan order-api-plan \
  --runtime "DOTNET|8.0"

# Configure app settings
az webapp config appsettings set \
  --name order-processing-api \
  --resource-group order-processing-rg \
  --settings \
    CosmosDb__ConnectionString="..." \
    CosmosDb__DatabaseName="OrderProcessingDB" \
    CosmosDb__ContainerName="Orders" \
    EventGrid__TopicEndpoint="..." \
    EventGrid__TopicKey="..."

# Deploy
cd src/OrderProcessingSystem.Api
dotnet publish -c Release
az webapp deployment source config-zip \
  --resource-group order-processing-rg \
  --name order-processing-api \
  --src publish.zip
```

### Step 9: Deploy Worker Service

```bash
# Create Web App for Worker
az webapp create \
  --name order-processing-worker \
  --resource-group order-processing-rg \
  --plan order-worker-plan \
  --runtime "DOTNET|8.0"

# Configure app settings
az webapp config appsettings set \
  --name order-processing-worker \
  --resource-group order-processing-rg \
  --settings \
    EventHub__ConnectionString="..." \
    EventHub__Name="order-events-hub" \
    EventHub__ConsumerGroup="$Default" \
    BlobStorage__ConnectionString="..." \
    BlobStorage__ContainerName="eventhub-checkpoints" \
    CosmosDb__ConnectionString="..." \
    CosmosDb__DatabaseName="OrderProcessingDB" \
    CosmosDb__ContainerName="Orders"

# Deploy
cd src/OrderProcessingSystem.Worker
dotnet publish -c Release
az webapp deployment source config-zip \
  --resource-group order-processing-rg \
  --name order-processing-worker \
  --src publish.zip
```

### Step 10: Configure API Management

```bash
# Create APIM instance
az apim create \
  --name order-processing-apim \
  --resource-group order-processing-rg \
  --location eastus \
  --publisher-name "Your Name" \
  --publisher-email "your-email@example.com" \
  --sku-name Developer \
  --sku-capacity 1

# Import API
az apim api import \
  --resource-group order-processing-rg \
  --service-name order-processing-apim \
  --api-id orders-api \
  --path orders \
  --specification-format OpenApi \
  --specification-url https://order-processing-api.azurewebsites.net/swagger/v1/swagger.json
```

#### APIM Policies

Add the following policies in Azure Portal:

**Subscription Key Policy:**
```xml
<policies>
  <inbound>
    <check-header name="Ocp-Apim-Subscription-Key" failed-check-httpcode="401" failed-check-error-message="Subscription key is required" />
  </inbound>
</policies>
```

**Rate Limiting Policy:**
```xml
<policies>
  <inbound>
    <rate-limit calls="5" renewal-period="60" />
  </inbound>
</policies>
```

**Logging Policy:**
```xml
<policies>
  <inbound>
    <log-to-eventhub logger-id="logger">
      <@(context.Request.Body.As<string>()) />
    </log-to-eventhub>
  </inbound>
</policies>
```

## Error Handling & Retry Strategy

### Retry Policy
- **Max Retries:** 3 attempts
- **Backoff Strategy:** Exponential backoff (2^retryAttempt * delaySeconds)
- **Initial Delay:** 2 seconds
- **Applies To:** Cosmos DB operations and Event Grid publishing

### Error Handling
- Comprehensive try-catch blocks
- Proper logging at all levels
- Graceful degradation
- User-friendly error messages

## Testing

### Using Postman
Import the `Postman_Collection.json` file into Postman for ready-to-use API requests.

### Manual Testing
1. Create an order via API
2. Verify order in Cosmos DB
3. Check Event Grid for published event
4. Verify Event Hub receives the event
5. Confirm Worker processes and updates order status

## Project Structure

```
OrderProcessingSystem/
├── src/
│   ├── OrderProcessingSystem.Api/
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── Program.cs
│   ├── OrderProcessingSystem.Worker/
│   │   ├── Services/
│   │   └── Program.cs
│   └── OrderProcessingSystem.Models/
├── frontend/
│   ├── index.html
│   ├── css/
│   └── js/
├── docs/
│   └── Architecture.md
└── README.md
```

## License

This project is provided as-is for educational and demonstration purposes.

