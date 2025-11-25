# Deployment Guide

## Prerequisites Checklist

- [ ] Azure Subscription with active billing
- [ ] Azure CLI installed and configured
- [ ] .NET 8 SDK installed
- [ ] Git (optional, for version control)

## Step-by-Step Deployment

### 1. Azure Resource Group Setup

```bash
# Login to Azure
az login

# Set subscription (if multiple)
az account set --subscription "Your Subscription ID"

# Create resource group
az group create \
  --name order-processing-rg \
  --location eastus
```

### 2. Cosmos DB Deployment

```bash
# Create Cosmos DB account
az cosmosdb create \
  --name order-processing-cosmos \
  --resource-group order-processing-rg \
  --default-consistency-level Session \
  --locations regionName=eastus failoverPriority=0

# Create database
az cosmosdb sql database create \
  --account-name order-processing-cosmos \
  --resource-group order-processing-rg \
  --name OrderProcessingDB

# Create container with partition key
az cosmosdb sql container create \
  --account-name order-processing-cosmos \
  --resource-group order-processing-rg \
  --database-name OrderProcessingDB \
  --name Orders \
  --partition-key-path "/id" \
  --throughput 400

# Get connection string (save this!)
az cosmosdb keys list \
  --name order-processing-cosmos \
  --resource-group order-processing-rg \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv
```

### 3. Event Grid Topic Setup

```bash
# Create Event Grid custom topic
az eventgrid topic create \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --location eastus

# Get topic endpoint (save this!)
az eventgrid topic show \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --query "endpoint" \
  --output tsv

# Get topic access key (save this!)
az eventgrid topic key list \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --query "key1" \
  --output tsv
```

### 4. Event Hub Namespace and Hub

```bash
# Create Event Hub namespace
az eventhubs namespace create \
  --name order-events-namespace \
  --resource-group order-processing-rg \
  --location eastus \
  --sku Standard \
  --enable-auto-inflate false \
  --maximum-throughput-units 0

# Create Event Hub
az eventhubs eventhub create \
  --name order-events-hub \
  --namespace-name order-events-namespace \
  --resource-group order-processing-rg \
  --partition-count 2 \
  --message-retention 1

# Get connection string (save this!)
az eventhubs namespace authorization-rule keys list \
  --name RootManageSharedAccessKey \
  --namespace-name order-events-namespace \
  --resource-group order-processing-rg \
  --query primaryConnectionString \
  --output tsv
```

### 5. Blob Storage for Checkpoints

```bash
# Create storage account
az storage account create \
  --name orderprocessingstorage \
  --resource-group order-processing-rg \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2

# Create container for checkpoints
az storage container create \
  --name eventhub-checkpoints \
  --account-name orderprocessingstorage \
  --public-access off

# Get connection string (save this!)
az storage account show-connection-string \
  --name orderprocessingstorage \
  --resource-group order-processing-rg \
  --query connectionString \
  --output tsv
```

### 6. Event Grid → Event Hub Subscription

```bash
# Get subscription ID
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

# Create Event Grid subscription
az eventgrid event-subscription create \
  --name order-events-subscription \
  --source-resource-id /subscriptions/$SUBSCRIPTION_ID/resourceGroups/order-processing-rg/providers/Microsoft.EventGrid/topics/order-events-topic \
  --endpoint-type eventhub \
  --endpoint /subscriptions/$SUBSCRIPTION_ID/resourceGroups/order-processing-rg/providers/Microsoft.EventHub/namespaces/order-events-namespace/eventhubs/order-events-hub \
  --event-delivery-schema eventgridschema
```

### 7. App Service Plans

```bash
# Create App Service Plan for API
az appservice plan create \
  --name order-api-plan \
  --resource-group order-processing-rg \
  --location eastus \
  --sku B1 \
  --is-linux

# Create App Service Plan for Worker
az appservice plan create \
  --name order-worker-plan \
  --resource-group order-processing-rg \
  --location eastus \
  --sku B1 \
  --is-linux
```

### 8. Deploy Web API

```bash
# Create Web App
az webapp create \
  --name order-processing-api \
  --resource-group order-processing-rg \
  --plan order-api-plan \
  --runtime "DOTNET|8.0"

# Configure app settings (replace placeholders with actual values)
az webapp config appsettings set \
  --name order-processing-api \
  --resource-group order-processing-rg \
  --settings \
    CosmosDb__ConnectionString="YOUR_COSMOS_CONNECTION_STRING" \
    CosmosDb__DatabaseName="OrderProcessingDB" \
    CosmosDb__ContainerName="Orders" \
    EventGrid__TopicEndpoint="YOUR_EVENT_GRID_ENDPOINT" \
    EventGrid__TopicKey="YOUR_EVENT_GRID_KEY" \
    ASPNETCORE_ENVIRONMENT="Production"

# Build and publish
cd src/OrderProcessingSystem.Api
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../../../api-deploy.zip .
cd ../../..

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group order-processing-rg \
  --name order-processing-api \
  --src api-deploy.zip
```

### 9. Deploy Worker Service

```bash
# Create Web App for Worker
az webapp create \
  --name order-processing-worker \
  --resource-group order-processing-rg \
  --plan order-worker-plan \
  --runtime "DOTNET|8.0"

# Configure app settings (replace placeholders)
az webapp config appsettings set \
  --name order-processing-worker \
  --resource-group order-processing-rg \
  --settings \
    EventHub__ConnectionString="YOUR_EVENTHUB_CONNECTION_STRING" \
    EventHub__Name="order-events-hub" \
    EventHub__ConsumerGroup="$Default" \
    BlobStorage__ConnectionString="YOUR_BLOB_CONNECTION_STRING" \
    BlobStorage__ContainerName="eventhub-checkpoints" \
    CosmosDb__ConnectionString="YOUR_COSMOS_CONNECTION_STRING" \
    CosmosDb__DatabaseName="OrderProcessingDB" \
    CosmosDb__ContainerName="Orders" \
    ASPNETCORE_ENVIRONMENT="Production"

# Build and publish
cd src/OrderProcessingSystem.Worker
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../../../worker-deploy.zip .
cd ../../..

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group order-processing-rg \
  --name order-processing-worker \
  --src worker-deploy.zip
```

### 10. API Management Setup

```bash
# Create APIM instance (takes 30-40 minutes)
az apim create \
  --name order-processing-apim \
  --resource-group order-processing-rg \
  --location eastus \
  --publisher-name "Your Organization" \
  --publisher-email "your-email@example.com" \
  --sku-name Developer \
  --sku-capacity 1

# Wait for APIM to be ready, then import API
# Note: You'll need to export OpenAPI spec from your API first
# Or use the Swagger endpoint

# Get API URL
API_URL="https://order-processing-api.azurewebsites.net"

# Import API via Azure Portal or REST API
# Portal: APIM > APIs > Add API > OpenAPI > Import from URL
# URL: https://order-processing-api.azurewebsites.net/swagger/v1/swagger.json
```

#### APIM Policies Configuration

1. Navigate to Azure Portal > API Management > order-processing-apim
2. Go to APIs > orders-api
3. Click on "All operations" or specific operation
4. Click "Code view" in the Inbound processing section
5. Add policies:

**Subscription Key Check:**
```xml
<policies>
  <inbound>
    <check-header name="Ocp-Apim-Subscription-Key" failed-check-httpcode="401" failed-check-error-message="Subscription key is required" />
    <base />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
```

**Rate Limiting:**
```xml
<rate-limit calls="5" renewal-period="60" />
```

**Logging:**
```xml
<log-to-eventhub logger-id="logger">
  <@(context.Request.Body.As<string>()) />
</log-to-eventhub>
```

### 11. Get APIM Subscription Key

```bash
# List subscriptions
az apim subscription list \
  --resource-group order-processing-rg \
  --service-name order-processing-apim

# Get subscription key
az apim subscription show \
  --resource-group order-processing-rg \
  --service-name order-processing-apim \
  --id "YOUR_SUBSCRIPTION_ID" \
  --query primaryKey \
  --output tsv
```

### 12. Update Frontend Configuration

Update `frontend/js/app.js`:

```javascript
const API_BASE_URL = 'https://order-processing-apim.azure-api.net/orders/api';
```

And add subscription key handling:

```javascript
function getApiKey() {
    return 'YOUR_APIM_SUBSCRIPTION_KEY';
}
```

## Verification Steps

1. **Test API Directly:**
   ```bash
   curl -X POST https://order-processing-api.azurewebsites.net/api/orders \
     -H "Content-Type: application/json" \
     -d '{"customerName":"Test","customerEmail":"test@test.com","items":[],"shippingAddress":"123 Main St"}'
   ```

2. **Test via APIM:**
   ```bash
   curl -X POST https://order-processing-apim.azure-api.net/orders/api/orders \
     -H "Content-Type: application/json" \
     -H "Ocp-Apim-Subscription-Key: YOUR_KEY" \
     -d '{"customerName":"Test","customerEmail":"test@test.com","items":[],"shippingAddress":"123 Main St"}'
   ```

3. **Check Cosmos DB:**
   - Navigate to Azure Portal > Cosmos DB > Data Explorer
   - Verify orders are being created

4. **Check Event Grid:**
   - Navigate to Event Grid Topic > Metrics
   - Verify events are being published

5. **Check Event Hub:**
   - Navigate to Event Hub > Metrics
   - Verify events are being received

6. **Check Worker Logs:**
   ```bash
   az webapp log tail \
     --name order-processing-worker \
     --resource-group order-processing-rg
   ```

## Troubleshooting

### API Not Responding
- Check App Service logs: `az webapp log tail --name order-processing-api --resource-group order-processing-rg`
- Verify app settings are correct
- Check Cosmos DB connection

### Events Not Processing
- Verify Event Grid subscription is active
- Check Event Hub connection string
- Verify Worker app settings
- Check Blob Storage for checkpoints

### APIM Issues
- Verify subscription key is correct
- Check rate limiting policies
- Verify API import was successful

## Cost Optimization

- Use Basic tier for development
- Scale down App Service Plans when not in use
- Use Cosmos DB serverless for low traffic
- Consider using Consumption plan for Worker Service

## Security Best Practices

1. Use Key Vault for connection strings
2. Enable HTTPS only
3. Use Managed Identity where possible
4. Implement proper authentication/authorization
5. Regular security updates

## Cleanup

To delete all resources:

```bash
az group delete --name order-processing-rg --yes --no-wait
```

