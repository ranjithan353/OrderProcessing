# Azure Setup Steps - Quick Reference

This document provides a quick reference for setting up all Azure services required for the Order Processing System.

## Prerequisites

1. Azure Subscription (with billing enabled)
2. Azure CLI installed and configured
3. Appropriate permissions to create resources

## Step-by-Step Azure Setup

### 1. Login and Set Subscription

```bash
az login
az account set --subscription "Your Subscription Name or ID"
```

### 2. Create Resource Group

```bash
az group create \
  --name order-processing-rg \
  --location eastus
```

### 3. Create Cosmos DB Account

```bash
# Create account
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

# Create container
az cosmosdb sql container create \
  --account-name order-processing-cosmos \
  --resource-group order-processing-rg \
  --database-name OrderProcessingDB \
  --name Orders \
  --partition-key-path "/id" \
  --throughput 400

# Get connection string (SAVE THIS!)
az cosmosdb keys list \
  --name order-processing-cosmos \
  --resource-group order-processing-rg \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv
```

**Output:** Save the connection string for API and Worker configuration.

### 4. Create Event Grid Topic

```bash
# Create topic
az eventgrid topic create \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --location eastus

# Get endpoint (SAVE THIS!)
az eventgrid topic show \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --query "endpoint" \
  --output tsv

# Get access key (SAVE THIS!)
az eventgrid topic key list \
  --name order-events-topic \
  --resource-group order-processing-rg \
  --query "key1" \
  --output tsv
```

**Output:** Save both endpoint and key for API configuration.

### 5. Create Event Hub Namespace and Hub

```bash
# Create namespace
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
  --partition-count 2 \
  --message-retention 1

# Get connection string (SAVE THIS!)
az eventhubs namespace authorization-rule keys list \
  --name RootManageSharedAccessKey \
  --namespace-name order-events-namespace \
  --resource-group order-processing-rg \
  --query primaryConnectionString \
  --output tsv
```

**Output:** Save connection string for Worker configuration.

### 6. Create Storage Account for Checkpoints

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
  --account-name orderprocessingstorage \
  --public-access off

# Get connection string (SAVE THIS!)
az storage account show-connection-string \
  --name orderprocessingstorage \
  --resource-group order-processing-rg \
  --query connectionString \
  --output tsv
```

**Output:** Save connection string for Worker configuration.

### 7. Create Event Grid Subscription (Event Grid → Event Hub)

```bash
# Get subscription ID
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

# Create subscription
az eventgrid event-subscription create \
  --name order-events-subscription \
  --source-resource-id /subscriptions/$SUBSCRIPTION_ID/resourceGroups/order-processing-rg/providers/Microsoft.EventGrid/topics/order-events-topic \
  --endpoint-type eventhub \
  --endpoint /subscriptions/$SUBSCRIPTION_ID/resourceGroups/order-processing-rg/providers/Microsoft.EventHub/namespaces/order-events-namespace/eventhubs/order-events-hub \
  --event-delivery-schema eventgridschema
```

**Note:** This connects Event Grid to Event Hub automatically.

### 8. Create App Service Plans

```bash
# Plan for API
az appservice plan create \
  --name order-api-plan \
  --resource-group order-processing-rg \
  --location eastus \
  --sku B1 \
  --is-linux

# Plan for Worker
az appservice plan create \
  --name order-worker-plan \
  --resource-group order-processing-rg \
  --location eastus \
  --sku B1 \
  --is-linux
```

### 9. Create and Configure Web API App Service

```bash
# Create Web App
az webapp create \
  --name order-processing-api \
  --resource-group order-processing-rg \
  --plan order-api-plan \
  --runtime "DOTNET|8.0"

# Configure app settings (REPLACE VALUES!)
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
```

### 10. Create and Configure Worker App Service

```bash
# Create Web App
az webapp create \
  --name order-processing-worker \
  --resource-group order-processing-rg \
  --plan order-worker-plan \
  --runtime "DOTNET|8.0"

# Configure app settings (REPLACE VALUES!)
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
```

### 11. Create API Management Instance

```bash
# Create APIM (takes 30-40 minutes!)
az apim create \
  --name order-processing-apim \
  --resource-group order-processing-rg \
  --location eastus \
  --publisher-name "Your Organization" \
  --publisher-email "your-email@example.com" \
  --sku-name Developer \
  --sku-capacity 1
```

**Note:** APIM creation takes 30-40 minutes. You can continue with other tasks.

### 12. Import API into APIM

After APIM is created and your API is deployed:

1. Go to Azure Portal → API Management → order-processing-apim
2. Navigate to APIs → Add API
3. Select "OpenAPI" → "Import from URL"
4. Enter: `https://order-processing-api.azurewebsites.net/swagger/v1/swagger.json`
5. Set API URL suffix: `orders`
6. Click "Create"

### 13. Configure APIM Policies

1. In APIM, go to APIs → orders-api → All operations
2. Click "Code view" in Inbound processing
3. Add policies (see DEPLOYMENT.md for full policy XML)

**Key Policies:**
- Subscription key requirement
- Rate limiting (5 requests/minute)
- Request/response logging

### 14. Get APIM Subscription Key

```bash
# List subscriptions
az apim subscription list \
  --resource-group order-processing-rg \
  --service-name order-processing-apim

# Get subscription key (use subscription ID from above)
az apim subscription show \
  --resource-group order-processing-rg \
  --service-name order-processing-apim \
  --id "YOUR_SUBSCRIPTION_ID" \
  --query primaryKey \
  --output tsv
```

## Configuration Summary

Save all these values for configuration:

### API App Settings
- `CosmosDb__ConnectionString`: From Step 3
- `EventGrid__TopicEndpoint`: From Step 4
- `EventGrid__TopicKey`: From Step 4

### Worker App Settings
- `EventHub__ConnectionString`: From Step 5
- `BlobStorage__ConnectionString`: From Step 6
- `CosmosDb__ConnectionString`: From Step 3

### Frontend Configuration
- API Base URL: `https://order-processing-apim.azure-api.net/orders/api`
- APIM Subscription Key: From Step 14

## Verification Checklist

- [ ] Cosmos DB database and container created
- [ ] Event Grid topic created and accessible
- [ ] Event Hub namespace and hub created
- [ ] Storage account and container created
- [ ] Event Grid subscription created (Event Grid → Event Hub)
- [ ] App Service plans created
- [ ] API App Service created and configured
- [ ] Worker App Service created and configured
- [ ] APIM instance created
- [ ] API imported into APIM
- [ ] APIM policies configured
- [ ] APIM subscription key obtained

## Testing

1. **Test API directly:**
   ```bash
   curl -X POST https://order-processing-api.azurewebsites.net/api/orders \
     -H "Content-Type: application/json" \
     -d '{"customerName":"Test","customerEmail":"test@test.com","items":[{"productId":"1","productName":"Test","quantity":1,"unitPrice":10}],"shippingAddress":"123 Main St"}'
   ```

2. **Test via APIM:**
   ```bash
   curl -X POST https://order-processing-apim.azure-api.net/orders/api/orders \
     -H "Content-Type: application/json" \
     -H "Ocp-Apim-Subscription-Key: YOUR_KEY" \
     -d '{"customerName":"Test","customerEmail":"test@test.com","items":[{"productId":"1","productName":"Test","quantity":1,"unitPrice":10}],"shippingAddress":"123 Main St"}'
   ```

3. **Check Cosmos DB:** Verify order appears in Data Explorer
4. **Check Event Hub:** Verify events are received
5. **Check Worker Logs:** Verify order status updated to "Processed"

## Troubleshooting

### API Not Working
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

## Cost Estimation (Approximate Monthly)

- Cosmos DB (400 RU/s): ~$25
- Event Grid: ~$0.60 per million events
- Event Hub (Standard): ~$10
- Blob Storage: ~$0.02 per GB
- App Service (B1 x 2): ~$30
- APIM (Developer): ~$50

**Total:** ~$115/month (varies by usage)

## Cleanup

To delete all resources:

```bash
az group delete --name order-processing-rg --yes --no-wait
```

**Warning:** This will delete all resources in the resource group!

