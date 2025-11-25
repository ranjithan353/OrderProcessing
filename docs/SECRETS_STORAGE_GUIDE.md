# Where to Store Secrets (Without Key Vault)

Since you don't have Key Vault permissions, here's where to store all your connection strings and secrets in Azure.

## ✅ Recommended: App Service Configuration

**Location:** App Service → Configuration → Application Settings

### Why This Works:
- ✅ Encrypted at rest
- ✅ Only accessible to the App Service
- ✅ Can be updated without redeployment
- ✅ No additional permissions needed
- ✅ Free (included with App Service)

### How to Access:
1. Go to your App Service in Azure Portal
2. Click **"Configuration"** (left menu)
3. Click **"Application settings"** tab
4. Click **"+ New application setting"**
5. Add your connection strings here

---

## Storage Locations by Service

### 1. Web API App Service (`order-processing-api`)

**Go to:** App Service → Configuration → Application Settings

Store these settings:

| Setting Name | Value Source | Where to Get It |
|-------------|--------------|-----------------|
| `CosmosDb__ConnectionString` | Cosmos DB → Keys → Primary Connection String | Step 2.3 |
| `CosmosDb__DatabaseName` | `OrderProcessingDB` | You created this |
| `CosmosDb__ContainerName` | `Orders` | You created this |
| `EventGrid__TopicEndpoint` | Event Grid → Overview → Topic Endpoint | Step 3.2 |
| `EventGrid__TopicKey` | Event Grid → Access keys → Key 1 | Step 3.2 |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Standard setting |

**Path in Portal:**
```
Azure Portal → order-processing-api → Configuration → Application settings
```

---

### 2. Worker Service App Service (`order-processing-worker`)

**Go to:** App Service → Configuration → Application Settings

Store these settings:

| Setting Name | Value Source | Where to Get It |
|-------------|--------------|-----------------|
| `EventHub__ConnectionString` | Event Hub → Shared access policies → RootManageSharedAccessKey | Step 4.3 |
| `EventHub__Name` | `order-events-hub` | You created this |
| `EventHub__ConsumerGroup` | `$Default` | Default value |
| `BlobStorage__ConnectionString` | Storage Account → Access keys → key1 Connection string | Step 5.3 |
| `BlobStorage__ContainerName` | `eventhub-checkpoints` | You created this |
| `CosmosDb__ConnectionString` | Cosmos DB → Keys → Primary Connection String | Step 2.3 |
| `CosmosDb__DatabaseName` | `OrderProcessingDB` | You created this |
| `CosmosDb__ContainerName` | `Orders` | You created this |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Standard setting |

**Path in Portal:**
```
Azure Portal → order-processing-worker → Configuration → Application settings
```

---

### 3. Frontend Configuration

**File:** `frontend/js/app.js`

Store these values:

| Variable | Value | Where to Get It |
|----------|-------|-----------------|
| `API_BASE_URL` | APIM Gateway URL + path | APIM → Overview → Gateway URL + `/orders/api` |
| `getApiKey()` return value | APIM Subscription Key | APIM → Subscriptions → Primary key |

**Example:**
```javascript
const API_BASE_URL = 'https://order-processing-apim.azure-api.net/orders/api';

function getApiKey() {
    return 'abc123...'; // From APIM Subscriptions
}
```

---

## How to Find Connection Strings

### Cosmos DB Connection String

1. Go to **Cosmos DB account** → `order-processing-cosmos`
2. Click **"Keys"** (left menu)
3. Under **"Primary Connection String"**, click **"Copy"**
4. Paste into App Service Configuration

**Format:**
```
AccountEndpoint=https://order-processing-cosmos.documents.azure.com:443/;AccountKey=abc123...
```

---

### Event Grid Topic Endpoint and Key

1. Go to **Event Grid Topic** → `order-events-topic`
2. Click **"Overview"** → Copy **"Topic Endpoint"**
3. Click **"Access keys"** → Copy **"Key 1"**

**Format:**
- Endpoint: `https://order-events-topic.eastus-1.eventgrid.azure.net/api/events`
- Key: `abc123...`

---

### Event Hub Connection String

1. Go to **Event Hub Namespace** → `order-events-namespace`
2. Click **"Shared access policies"** (left menu)
3. Click **"RootManageSharedAccessKey"**
4. Copy **"Primary Connection String"**

**Format:**
```
Endpoint=sb://order-events-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123...
```

---

### Storage Account Connection String

1. Go to **Storage Account** → `orderprocessingstorage`
2. Click **"Access keys"** (left menu)
3. Under **"key1"**, click **"Show"** next to Connection string
4. Click **"Copy"**

**Format:**
```
DefaultEndpointsProtocol=https;AccountName=orderprocessingstorage;AccountKey=abc123...;EndpointSuffix=core.windows.net
```

---

### APIM Subscription Key

1. Go to **API Management** → `order-processing-apim`
2. Click **"Subscriptions"** (left menu)
3. Click on your subscription
4. Copy **"Primary key"**

**Format:**
```
abc123def456...
```

---

## Security Best Practices (Without Key Vault)

### ✅ DO:

1. **Store in App Service Configuration**
   - Encrypted at rest
   - Only accessible to the App Service
   - Can be rotated easily

2. **Use Separate App Services**
   - API and Worker have separate configurations
   - Limits exposure if one is compromised

3. **Enable HTTPS Only**
   - In App Service → TLS/SSL settings
   - Force HTTPS redirects

4. **Rotate Keys Regularly**
   - Update connection strings in App Service Configuration
   - Restart App Service after updating

5. **Use Managed Identity (If Available)**
   - For services that support it
   - No connection strings needed

### ❌ DON'T:

1. **Don't commit to code**
   - Never put connection strings in source code
   - Use `.gitignore` to exclude `appsettings.Production.json`

2. **Don't share in documentation**
   - Remove connection strings from shared docs
   - Use placeholders in examples

3. **Don't use default keys**
   - Regenerate keys after setup
   - Use different keys for different environments

4. **Don't expose in logs**
   - Be careful with logging
   - Don't log connection strings

---

## Alternative Storage Options (If Needed)

### Option 1: Environment Variables (Local Development)
- Store in `appsettings.Development.json`
- **Never commit this file!**
- Add to `.gitignore`

### Option 2: User Secrets (Local Development)
```bash
dotnet user-secrets set "CosmosDb:ConnectionString" "your-connection-string"
```
- Only for local development
- Stored in user profile

### Option 3: Azure App Configuration (Advanced)
- Separate service for configuration
- Requires additional setup
- More features than App Service Configuration

---

## Quick Reference: All Connection Strings

Save this checklist as you gather them:

```
✅ Cosmos DB Connection String: _________________________
✅ Event Grid Topic Endpoint: _________________________
✅ Event Grid Topic Key: _________________________
✅ Event Hub Connection String: _________________________
✅ Storage Account Connection String: _________________________
✅ APIM Subscription Key: _________________________
✅ APIM Gateway URL: _________________________
```

---

## Updating Connection Strings

### To Update a Connection String:

1. Go to App Service → **Configuration**
2. Find the setting you want to update
3. Click on it → Edit the value
4. Click **"OK"**
5. Click **"Save"** at the top
6. Click **"Continue"** to restart the app

**Note:** App will restart automatically after saving.

---

## Summary

**For this project, store all secrets in:**
- ✅ **App Service Configuration** (for API and Worker)
- ✅ **JavaScript file** (for frontend - APIM key only)

**No Key Vault needed!** App Service Configuration provides:
- Encryption at rest
- Secure access
- Easy updates
- No additional cost
- No special permissions required

