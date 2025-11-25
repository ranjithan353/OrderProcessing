# Azure Manual Setup Guide (Azure Portal - No Key Vault)

This guide provides step-by-step instructions to set up all Azure services **manually through the Azure Portal** without using Key Vault. All connection strings and secrets will be stored in **App Service Configuration** settings.

## Prerequisites

- Azure Subscription with active billing
- Access to Azure Portal (portal.azure.com)
- Basic understanding of Azure services

---

## Step 1: Create Resource Group

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **"Create a resource"** (top left)
3. Search for **"Resource group"**
4. Click **"Create"**
5. Fill in:
   - **Subscription**: Select your subscription
   - **Resource group**: `order-processing-rg`
   - **Region**: `East US` (or your preferred region)
6. Click **"Review + create"**
7. Click **"Create"**

**Note:** Save this resource group name - all resources will be created here.

---

## Step 2: Create Cosmos DB Account

### 2.1 Create Cosmos DB Account

1. In Azure Portal, click **"Create a resource"**
2. Search for **"Azure Cosmos DB"**
3. Click **"Create"**
4. Select **"Core (SQL) - Recommended"**
5. Click **"Create"**
6. Fill in the **Basics** tab:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg` (select existing)
   - **Account Name**: `order-processing-cosmos` (must be globally unique)
   - **Location**: `East US` (same as resource group)
   - **Capacity mode**: `Provisioned throughput`
   - **Apply Free Tier Discount**: Leave unchecked (or check if eligible)
7. Click **"Review + create"**
8. Click **"Create"**
9. **Wait 5-10 minutes** for deployment to complete

### 2.2 Create Database and Container

1. Go to your Cosmos DB account: `order-processing-cosmos`
2. In left menu, click **"Data Explorer"**
3. Click **"New Container"**
4. Fill in:
   - **Database id**: `OrderProcessingDB` (create new)
   - **Container id**: `Orders`
   - **Partition key**: `/id`
   - **Throughput**: `400` (or use "Autoscale" with 400-4000)
5. Click **"OK"**

### 2.3 Get Connection String

1. In Cosmos DB account, go to **"Keys"** (left menu)
2. Under **"Primary Connection String"**, click **"Copy"**
3. **SAVE THIS** - You'll need it later!
   - Format: `AccountEndpoint=https://order-processing-cosmos.documents.azure.com:443/;AccountKey=...`

---

## Step 3: Create Event Grid Topic

### 3.1 Create Custom Topic

1. Click **"Create a resource"**
2. Search for **"Event Grid Topic"**
3. Click **"Create"**
4. Fill in:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg`
   - **Name**: `order-events-topic`
   - **Event Schema**: `Event Grid Schema`
   - **Location**: `East US`
5. Click **"Review + create"**
6. Click **"Create"**
7. Wait 2-3 minutes for deployment

### 3.2 Get Topic Endpoint and Key

1. Go to your Event Grid topic: `order-events-topic`
2. In left menu, click **"Overview"**
3. Copy the **"Topic Endpoint"** - **SAVE THIS!**
   - Format: `https://order-events-topic.eastus-1.eventgrid.azure.net/api/events`
4. In left menu, click **"Access keys"**
5. Copy **"Key 1"** - **SAVE THIS!**

---

## Step 4: Create Event Hub Namespace and Hub

### 4.1 Create Event Hub Namespace

1. Click **"Create a resource"**
2. Search for **"Event Hubs"**
3. Click **"Create"**
4. Fill in:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg`
   - **Namespace name**: `order-events-namespace` (must be globally unique)
   - **Location**: `East US`
   - **Pricing tier**: `Standard`
5. Click **"Review + create"**
6. Click **"Create"**
7. Wait 5-10 minutes for deployment

### 4.2 Create Event Hub

1. Go to your Event Hub namespace: `order-events-namespace`
2. In left menu, click **"Event hubs"**
3. Click **"+ Event Hub"**
4. Fill in:
   - **Name**: `order-events-hub`
   - **Partition count**: `2`
   - **Message retention**: `1` day
   - **Capture**: Leave disabled
5. Click **"Create"**

### 4.3 Get Connection String

1. In Event Hub namespace, go to **"Shared access policies"** (left menu)
2. Click on **"RootManageSharedAccessKey"**
3. Copy **"Primary Connection String"** - **SAVE THIS!**
   - Format: `Endpoint=sb://order-events-namespace.servicebus.windows.net/;SharedAccessKeyName=...`

---

## Step 5: Create Storage Account for Checkpoints

### 5.1 Create Storage Account

1. Click **"Create a resource"**
2. Search for **"Storage account"**
3. Click **"Create"**
4. Fill in **Basics** tab:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg`
   - **Storage account name**: `orderprocessingstorage` (must be globally unique, lowercase, no hyphens)
   - **Region**: `East US`
   - **Performance**: `Standard`
   - **Redundancy**: `Locally-redundant storage (LRS)`
5. Click **"Review + create"**
6. Click **"Create"**
7. Wait 2-3 minutes

### 5.2 Create Container

1. Go to your storage account: `orderprocessingstorage`
2. In left menu, click **"Containers"**
3. Click **"+ Container"**
4. Fill in:
   - **Name**: `eventhub-checkpoints`
   - **Public access level**: `Private (no anonymous access)`
5. Click **"Create"**

### 5.3 Get Connection String

1. In storage account, go to **"Access keys"** (left menu)
2. Under **"key1"**, click **"Show"** next to Connection string
3. Click **"Copy"** - **SAVE THIS!**
   - Format: `DefaultEndpointsProtocol=https;AccountName=orderprocessingstorage;AccountKey=...`

---

## Step 6: Create Event Grid Subscription (Event Grid → Event Hub)

1. Go to your Event Grid topic: `order-events-topic`
2. In left menu, click **"+ Event Subscription"**
3. Fill in **Basics**:
   - **Name**: `order-events-subscription`
   - **Event Schema**: `Event Grid Schema`
4. Click **"Next: Event Types"** (leave defaults)
5. Click **"Next: Endpoint Details"**
6. Select **"Event Hub"** as Endpoint type
7. Click **"Select an endpoint"**
8. Select:
   - **Subscription**: Your subscription
   - **Namespace**: `order-events-namespace`
   - **Event Hub**: `order-events-hub`
   - **Event Hub consumer group**: `$Default`
9. Click **"Confirm selection"**
10. Click **"Create"**

**This connects Event Grid to Event Hub automatically!**

---

## Step 7: Create App Service Plans

### 7.1 Create Plan for API

1. Click **"Create a resource"**
2. Search for **"App Service Plan"**
3. Click **"Create"**
4. Fill in:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg`
   - **Name**: `order-api-plan`
   - **Operating System**: `Linux`
   - **Region**: `East US`
   - **Pricing tier**: Click **"Change size"**
     - Select **"Dev/Test"** tab
     - Choose **"B1 Basic"** (or F1 Free for testing)
     - Click **"Apply"**
5. Click **"Review + create"**
6. Click **"Create"**
7. Wait 2-3 minutes

### 7.2 Create Plan for Worker

1. Repeat steps above with:
   - **Name**: `order-worker-plan`
   - Same settings as API plan
2. Click **"Create"**
3. Wait 2-3 minutes

---

## Step 8: Create and Deploy Web API App Service

### 8.1 Create Web App

1. Click **"Create a resource"**
2. Search for **"Web App"**
3. Click **"Create"**
4. Fill in **Basics**:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg`
   - **Name**: `order-processing-api` (must be globally unique)
   - **Publish**: `Code`
   - **Runtime stack**: `.NET 8`
   - **Operating System**: `Linux`
   - **Region**: `East US`
   - **App Service Plan**: `order-api-plan` (select existing)
5. Click **"Review + create"**
6. Click **"Create"**
7. Wait 2-3 minutes

### 8.2 Configure App Settings (Connection Strings)

1. Go to your Web App: `order-processing-api`
2. In left menu, click **"Configuration"**
3. Click **"+ New application setting"**
4. Add the following settings one by one:

   **Setting 1:**
   - **Name**: `CosmosDb__ConnectionString`
   - **Value**: (Paste your Cosmos DB connection string from Step 2.3)
   - Click **"OK"**

   **Setting 2:**
   - **Name**: `CosmosDb__DatabaseName`
   - **Value**: `OrderProcessingDB`
   - Click **"OK"**

   **Setting 3:**
   - **Name**: `CosmosDb__ContainerName`
   - **Value**: `Orders`
   - Click **"OK"**

   **Setting 4:**
   - **Name**: `EventGrid__TopicEndpoint`
   - **Value**: (Paste your Event Grid topic endpoint from Step 3.2)
   - Click **"OK"**

   **Setting 5:**
   - **Name**: `EventGrid__TopicKey`
   - **Value**: (Paste your Event Grid key from Step 3.2)
   - Click **"OK"**

   **Setting 6:**
   - **Name**: `ASPNETCORE_ENVIRONMENT`
   - **Value**: `Production`
   - Click **"OK"**

5. After adding all settings, click **"Save"** at the top
6. Click **"Continue"** when prompted to restart

### 8.3 Deploy Your Code

**Option A: Using Visual Studio (Recommended)**

1. Open your solution in Visual Studio
2. Right-click on `OrderProcessingSystem.Api` project
3. Select **"Publish"**
4. Click **"Create new profile"**
5. Select **"Azure"** → **"Azure App Service (Linux)"**
6. Select your subscription and resource group
7. Select `order-processing-api` app
8. Click **"Finish"**
9. Click **"Publish"**

**Option B: Using VS Code**

1. Install **"Azure App Service"** extension
2. Right-click on `src/OrderProcessingSystem.Api` folder
3. Select **"Deploy to Web App"**
4. Select `order-processing-api`
5. Wait for deployment

**Option C: Using Azure Portal (ZIP Deploy)**

1. Build your project locally:
   ```bash
   cd src/OrderProcessingSystem.Api
   dotnet publish -c Release -o ./publish
   ```
2. Zip the `publish` folder contents
3. In Azure Portal, go to your Web App
4. In left menu, click **"Deployment Center"**
5. Select **"Local Git"** or **"ZIP Deploy"**
6. Upload your zip file

---

## Step 9: Create and Deploy Worker Service App Service

### 9.1 Create Web App for Worker

1. Click **"Create a resource"**
2. Search for **"Web App"**
3. Click **"Create"**
4. Fill in:
   - **Name**: `order-processing-worker` (must be globally unique)
   - **Resource Group**: `order-processing-rg`
   - **Runtime stack**: `.NET 8`
   - **Operating System**: `Linux`
   - **App Service Plan**: `order-worker-plan` (select existing)
5. Click **"Review + create"**
6. Click **"Create"**
7. Wait 2-3 minutes

### 9.2 Configure App Settings

1. Go to your Web App: `order-processing-worker`
2. In left menu, click **"Configuration"**
3. Add the following settings:

   **Setting 1:**
   - **Name**: `EventHub__ConnectionString`
   - **Value**: (Paste Event Hub connection string from Step 4.3)
   - Click **"OK"**

   **Setting 2:**
   - **Name**: `EventHub__Name`
   - **Value**: `order-events-hub`
   - Click **"OK"**

   **Setting 3:**
   - **Name**: `EventHub__ConsumerGroup`
   - **Value**: `$Default`
   - Click **"OK"**

   **Setting 4:**
   - **Name**: `BlobStorage__ConnectionString`
   - **Value**: (Paste Storage account connection string from Step 5.3)
   - Click **"OK"**

   **Setting 5:**
   - **Name**: `BlobStorage__ContainerName`
   - **Value**: `eventhub-checkpoints`
   - Click **"OK"**

   **Setting 6:**
   - **Name**: `CosmosDb__ConnectionString`
   - **Value**: (Paste Cosmos DB connection string from Step 2.3)
   - Click **"OK"**

   **Setting 7:**
   - **Name**: `CosmosDb__DatabaseName`
   - **Value**: `OrderProcessingDB`
   - Click **"OK"**

   **Setting 8:**
   - **Name**: `CosmosDb__ContainerName`
   - **Value**: `Orders`
   - Click **"OK"**

   **Setting 9:**
   - **Name**: `ASPNETCORE_ENVIRONMENT`
   - **Value**: `Production`
   - Click **"OK"**

4. Click **"Save"** at the top
5. Click **"Continue"** to restart

### 9.3 Deploy Worker Code

Use the same deployment method as Step 8.3, but deploy the `OrderProcessingSystem.Worker` project to `order-processing-worker` app.

---

## Step 10: Create API Management (APIM)

### 10.1 Create APIM Instance

1. Click **"Create a resource"**
2. Search for **"API Management"**
3. Click **"Create"**
4. Fill in **Basics**:
   - **Subscription**: Your subscription
   - **Resource Group**: `order-processing-rg`
   - **Region**: `East US`
   - **Resource name**: `order-processing-apim` (must be globally unique)
   - **Organization name**: Your organization name
   - **Administrator email**: Your email address
   - **Pricing tier**: `Developer` (cheapest option)
5. Click **"Review + create"**
6. Click **"Create"**
7. **WAIT 30-40 MINUTES** for deployment (APIM takes a long time!)

### 10.2 Import API into APIM

1. Go to your APIM instance: `order-processing-apim`
2. In left menu, click **"APIs"**
3. Click **"+ Add API"**
4. Select **"OpenAPI"**
5. Select **"Import from URL"**
6. Enter:
   - **OpenAPI specification URL**: `https://order-processing-api.azurewebsites.net/swagger/v1/swagger.json`
   - **Display name**: `Orders API`
   - **Name**: `orders-api`
   - **API URL suffix**: `orders`
7. Click **"Create"**

### 10.3 Configure APIM Policies

1. In APIM, go to **"APIs"** → **"orders-api"**
2. Click **"All operations"**
3. In the **"Inbound processing"** section, click **"</> Code view"**
4. Add the following policies:

**Subscription Key Policy:**
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

**Rate Limiting Policy (add after check-header):**
```xml
<rate-limit calls="5" renewal-period="60" />
```

5. Click **"Save"**

### 10.4 Get Subscription Key

1. In APIM, go to **"Subscriptions"** (left menu)
2. You'll see a default subscription (or create one)
3. Click on the subscription
4. Copy the **"Primary key"** - **SAVE THIS!**
   - This is your APIM subscription key

### 10.5 Get APIM Gateway URL

1. In APIM, go to **"Overview"**
2. Copy the **"Gateway URL"**
   - Format: `https://order-processing-apim.azure-api.net`
3. Your API endpoint will be: `https://order-processing-apim.azure-api.net/orders/api`

---

## Step 11: Update Frontend Configuration

1. Open `frontend/js/app.js`
2. Update the API URL:

**For Direct API (without APIM):**
```javascript
const API_BASE_URL = 'https://order-processing-api.azurewebsites.net/api';
```

**For APIM (recommended):**
```javascript
const API_BASE_URL = 'https://order-processing-apim.azure-api.net/orders/api';
```

3. Update the `getApiKey()` function:
```javascript
function getApiKey() {
    return 'YOUR_APIM_SUBSCRIPTION_KEY'; // Paste from Step 10.4
}
```

4. Save the file

---

## Step 12: Test Your Deployment

### 12.1 Test API Directly

1. Go to: `https://order-processing-api.azurewebsites.net/swagger`
2. You should see Swagger UI
3. Test the endpoints

### 12.2 Test via APIM

1. Use Postman or curl:
```bash
curl -X POST https://order-processing-apim.azure-api.net/orders/api/orders \
  -H "Content-Type: application/json" \
  -H "Ocp-Apim-Subscription-Key: YOUR_KEY" \
  -d '{
    "customerName": "Test User",
    "customerEmail": "test@example.com",
    "shippingAddress": "123 Test St",
    "items": [{
      "productId": "PROD-001",
      "productName": "Test Product",
      "quantity": 1,
      "unitPrice": 10.00
    }]
  }'
```

### 12.3 Verify End-to-End Flow

1. **Create an order** via API
2. **Check Cosmos DB**: Go to Data Explorer → Verify order exists
3. **Check Event Grid**: Go to Metrics → Verify events published
4. **Check Event Hub**: Go to Metrics → Verify events received
5. **Check Worker Logs**: 
   - Go to `order-processing-worker` App Service
   - Click **"Log stream"** (left menu)
   - Verify order processing logs
6. **Check Order Status**: Query order again - should be "Processed"

---

## Important Notes

### Where Connection Strings Are Stored

**Instead of Key Vault, we use:**
- **App Service Configuration** → **Application Settings**
- These are encrypted at rest
- Accessible only to the App Service
- Can be updated without redeployment

### Security Best Practices

1. **Never commit connection strings to code**
2. **Use App Service Configuration** (as shown above)
3. **Enable HTTPS only** in App Service settings
4. **Use Managed Identity** (advanced - requires permissions)
5. **Rotate keys regularly** from Azure Portal

### Cost Optimization

- Use **F1 Free tier** for App Service Plans (limited hours)
- Use **Basic B1** for production ($13/month each)
- Cosmos DB: Start with 400 RU/s (~$25/month)
- Event Hub Standard: ~$10/month
- APIM Developer: ~$50/month

**Total estimated cost: ~$100-150/month**

---

## Troubleshooting

### API Not Working
1. Check App Service is running (Overview page)
2. Check Configuration settings are correct
3. View **"Log stream"** for errors
4. Check **"Application Insights"** if enabled

### Events Not Processing
1. Verify Event Grid subscription is active
2. Check Event Hub connection string
3. Verify Worker app settings
4. Check Worker logs in Log stream

### CORS Errors
1. Update CORS in `Program.cs` to include your frontend URL
2. Redeploy API
3. Or disable CORS for testing (not recommended for production)

### APIM Issues
1. Verify subscription key is correct
2. Check API is imported correctly
3. Verify policies are saved
4. Check APIM logs

---

## Summary Checklist

- [ ] Resource group created
- [ ] Cosmos DB created (database + container)
- [ ] Event Grid topic created
- [ ] Event Hub namespace + hub created
- [ ] Storage account + container created
- [ ] Event Grid subscription created (Event Grid → Event Hub)
- [ ] App Service plans created (API + Worker)
- [ ] API App Service created + configured + deployed
- [ ] Worker App Service created + configured + deployed
- [ ] APIM created + API imported + policies configured
- [ ] Frontend updated with API URL
- [ ] End-to-end testing completed

---

## Next Steps

1. Test all endpoints
2. Monitor costs in Azure Cost Management
3. Set up alerts for errors
4. Consider adding Application Insights for monitoring
5. Review security settings

**All connection strings are stored in App Service Configuration - no Key Vault needed!**

