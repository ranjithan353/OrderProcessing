# Azure Connection Strings and Keys Reference

This document lists all Azure connection strings and keys required for the Order Processing System.

## 📋 Quick Reference Checklist

### For API Project (`appsettings.json`)
- [ ] Cosmos DB Connection String
- [ ] Event Grid Topic Endpoint
- [ ] Event Grid Topic Key

### For Worker Project (`appsettings.json`)
- [ ] Cosmos DB Connection String
- [ ] Event Hub Connection String
- [ ] Event Hub Name
- [ ] Blob Storage Connection String
- [ ] Blob Container Name

---

## 🔧 Configuration Details

### 1. Azure Cosmos DB

**Used in:** Both API and Worker projects

**Configuration Key:** `CosmosDb:ConnectionString`

**Where to Find:**
1. Go to Azure Portal → Your Cosmos DB Account
2. Navigate to **"Keys"** in the left menu
3. Copy the **"Primary Connection String"** or **"Secondary Connection String"**

**Format:**
```
AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=your-account-key==;Database=OrderProcessingDB
```

**Additional Settings:**
- `CosmosDb:DatabaseName` = `OrderProcessingDB` (default)
- `CosmosDb:ContainerName` = `Orders` (default)

---

### 2. Azure Event Grid

**Used in:** API project only

**Configuration Keys:**
- `EventGrid:TopicEndpoint`
- `EventGrid:TopicKey`

**Where to Find:**

**Topic Endpoint:**
1. Go to Azure Portal → Your Event Grid Topic
2. Navigate to **"Overview"**
3. Copy the **"Topic Endpoint"** URL
   - Format: `https://your-topic-name.region.eventgrid.azure.net/api/events`

**Topic Key:**
1. Go to Azure Portal → Your Event Grid Topic
2. Navigate to **"Access keys"** in the left menu
3. Copy **"Key 1"** or **"Key 2"**

---

### 3. Azure Event Hub

**Used in:** Worker project only

**Configuration Keys:**
- `EventHub:ConnectionString`
- `EventHub:Name`
- `EventHub:ConsumerGroup` (optional, defaults to `$Default`)

**Where to Find:**

**Connection String:**
1. Go to Azure Portal → Your Event Hub Namespace
2. Navigate to **"Shared access policies"** in the left menu
3. Click on **"RootManageSharedAccessKey"** (or create a new policy)
4. Copy the **"Connection string-primary key"** or **"Connection string-secondary key"**

**Event Hub Name:**
1. Go to Azure Portal → Your Event Hub Namespace
2. Navigate to **"Event Hubs"** in the left menu
3. Click on your Event Hub (e.g., `order-events-hub`)
4. Copy the **"Name"** from the Overview page

**Format:**
```
Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key==;EntityPath=order-events-hub
```

---

### 4. Azure Blob Storage

**Used in:** Worker project only (for Event Hub checkpointing)

**Configuration Keys:**
- `BlobStorage:ConnectionString`
- `BlobStorage:ContainerName` (optional, defaults to `eventhub-checkpoints`)

**Where to Find:**

**Connection String:**
1. Go to Azure Portal → Your Storage Account
2. Navigate to **"Access keys"** in the left menu
3. Copy **"Connection string"** from either Key1 or Key2

**Format:**
```
DefaultEndpointsProtocol=https;AccountName=your-account-name;AccountKey=your-account-key==;EndpointSuffix=core.windows.net
```

**Container Name:**
- Default: `eventhub-checkpoints`
- You can create this container in your Storage Account → Containers

---

## 📝 Complete Configuration Files

### API Project (`src/OrderProcessingSystem.Api/appsettings.json`)

```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=your-key==;",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  },
  "EventGrid": {
    "TopicEndpoint": "https://your-topic-name.region.eventgrid.azure.net/api/events",
    "TopicKey": "your-event-grid-key-here"
  }
}
```

### Worker Project (`src/OrderProcessingSystem.Worker/appsettings.json`)

```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=your-key==;",
    "DatabaseName": "OrderProcessingDB",
    "ContainerName": "Orders"
  },
  "EventHub": {
    "ConnectionString": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key==;EntityPath=order-events-hub",
    "Name": "order-events-hub",
    "ConsumerGroup": "$Default"
  },
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=your-account;AccountKey=your-key==;EndpointSuffix=core.windows.net",
    "ContainerName": "eventhub-checkpoints"
  }
}
```

---

## 🔐 Security Best Practices

1. **Never commit connection strings to source control**
   - Use `appsettings.Development.json` for local development (add to `.gitignore`)
   - Use Azure App Service Configuration for production

2. **Use Managed Identity when possible** (for production)
   - Reduces the need to store connection strings
   - More secure than access keys

3. **Rotate keys regularly**
   - Update connection strings in Azure Portal
   - Update in your application configuration

4. **Use separate keys for different environments**
   - Development, Staging, Production should have separate Azure resources

---

## 📍 Azure Portal Navigation Paths

### Cosmos DB
```
Azure Portal → Cosmos DB accounts → [Your Account] → Keys
```

### Event Grid
```
Azure Portal → Event Grid Topics → [Your Topic] → Overview (for endpoint) or Access keys (for key)
```

### Event Hub
```
Azure Portal → Event Hubs → [Your Namespace] → Shared access policies → [Policy Name]
```

### Blob Storage
```
Azure Portal → Storage accounts → [Your Account] → Access keys
```

---

## ✅ Verification Checklist

After adding all connection strings:

- [ ] API project builds successfully
- [ ] Worker project builds successfully
- [ ] API can connect to Cosmos DB (check logs)
- [ ] API can publish to Event Grid (check logs)
- [ ] Worker can connect to Event Hub (check logs)
- [ ] Worker can connect to Blob Storage (check logs)
- [ ] Worker can read from Cosmos DB (check logs)

---

## 🆘 Troubleshooting

**Connection String Issues:**
- Ensure no extra spaces or line breaks
- Verify the connection string format matches the examples above
- Check that the resource exists in Azure Portal
- Verify your Azure subscription is active

**Authentication Errors:**
- Verify keys are correct (copy-paste carefully)
- Check if keys have been rotated (regenerate if needed)
- Ensure the resource is in the same region/subscription

**Missing Resources:**
- Create missing Azure resources first
- Refer to `docs/AZURE_MANUAL_SETUP.md` for setup instructions

