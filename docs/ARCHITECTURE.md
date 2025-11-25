# Architecture Documentation

## System Architecture

### High-Level Overview

The Order Processing System is an event-driven microservices architecture built on Azure cloud services, designed to handle order creation, processing, and status updates in a scalable and reliable manner.

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                            │
│  ┌──────────────────┐              ┌──────────────────┐         │
│  │   Web Frontend   │              │  Mobile/API      │         │
│  │   (Bootstrap)    │              │   Clients        │         │
│  └────────┬─────────┘              └────────┬─────────┘         │
└───────────┼──────────────────────────────────┼──────────────────┘
            │                                  │
            └──────────────┬───────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────┐
│                    API GATEWAY LAYER                             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         Azure API Management (APIM)                      │   │
│  │  • Subscription Key Authentication                       │   │
│  │  • Rate Limiting (5 requests/minute)                    │   │
│  │  • Request/Response Logging                              │   │
│  │  • Request Transformation                                │   │
│  └───────────────────────┬──────────────────────────────────┘   │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────┐
│                      API LAYER                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         .NET 8 Web API (App Service)                     │   │
│  │  • POST /api/orders - Create Order                       │   │
│  │  • GET /api/orders/{id} - Get Order                      │   │
│  │  • GET /api/orders - Get All Orders                     │   │
│  │                                                           │   │
│  │  Features:                                                │   │
│  │  • Retry Logic (Polly)                                   │   │
│  │  • Error Handling                                        │   │
│  │  • Request Validation                                    │   │
│  └───────┬───────────────────────────────┬──────────────────┘   │
└──────────┼───────────────────────────────┼───────────────────────┘
           │                               │
           │                               │
┌──────────▼──────────┐      ┌────────────▼──────────────┐
│   DATA LAYER        │      │    EVENT LAYER             │
│                     │      │                            │
│  ┌──────────────┐   │      │  ┌──────────────────────┐  │
│  │  Cosmos DB   │   │      │  │   Event Grid Topic   │  │
│  │  (SQL API)   │   │      │  │  order-events-topic  │  │
│  │              │   │      │  └──────────┬───────────┘  │
│  │  Database:   │   │      │             │              │
│  │  OrderProcDB │   │      │             │              │
│  │              │   │      │             ▼              │
│  │  Container:  │   │      │  ┌──────────────────────┐  │
│  │  Orders      │   │      │  │   Event Hub         │  │
│  │              │   │      │  │  order-events-hub   │  │
│  │  Partition:  │   │      │  └──────────┬───────────┘  │
│  │  /id         │   │      │             │              │
│  └──────────────┘   │      │             │              │
└─────────────────────┘      └─────────────┼──────────────┘
                                            │
┌──────────────────────────────────────────▼──────────────┐
│              PROCESSING LAYER                              │
│  ┌────────────────────────────────────────────────────┐   │
│  │    .NET 8 Worker Service (App Service)             │   │
│  │                                                     │   │
│  │  • Event Hub Consumer                               │   │
│  │  • Event Processing                                │   │
│  │  • Order Status Updates                            │   │
│  │  • Checkpoint Management (Blob Storage)            │   │
│  └───────────────────────┬─────────────────────────────┘   │
└──────────────────────────┼──────────────────────────────────┘
                           │
                           │
┌──────────────────────────▼──────────────────────────────────┐
│              STORAGE LAYER                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Blob Storage                                  │   │
│  │  • Event Hub Checkpoints                             │   │
│  │  • Container: eventhub-checkpoints                   │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

## Data Flow

### Order Creation Flow

1. **Client Request**
   - User submits order via frontend or API client
   - Request goes through APIM (subscription key validation, rate limiting)

2. **API Processing**
   - Web API receives request
   - Validates order data
   - Calculates total amount

3. **Data Persistence**
   - Order saved to Cosmos DB
   - Retry logic handles transient failures
   - Order status: "Created"

4. **Event Publishing**
   - Order.Created event published to Event Grid
   - Event includes order details
   - Retry logic ensures delivery

5. **Event Routing**
   - Event Grid routes event to Event Hub
   - Event stored in Event Hub partitions

6. **Event Processing**
   - Worker Service consumes event from Event Hub
   - Processes order (simulates business logic)
   - Updates order status to "Processed" in Cosmos DB
   - Checkpoint saved to Blob Storage

### Order Retrieval Flow

1. **Client Request**
   - GET request to /api/orders or /api/orders/{id}
   - Goes through APIM

2. **Data Retrieval**
   - API queries Cosmos DB
   - Returns order(s) to client

## Components Details

### 1. Web API (`OrderProcessingSystem.Api`)

**Responsibilities:**
- Handle HTTP requests
- Validate input data
- Persist orders to Cosmos DB
- Publish events to Event Grid
- Implement retry logic
- Error handling and logging

**Key Classes:**
- `OrdersController`: REST endpoints
- `CosmosDbService`: Database operations
- `EventGridService`: Event publishing
- `RetryPolicy`: Retry logic configuration

**Dependencies:**
- Microsoft.Azure.Cosmos
- Azure.Messaging.EventGrid
- Polly (retry logic)

### 2. Worker Service (`OrderProcessingSystem.Worker`)

**Responsibilities:**
- Consume events from Event Hub
- Process order events
- Update order status in Cosmos DB
- Manage checkpoints in Blob Storage
- Handle event processing errors

**Key Classes:**
- `EventHubProcessorService`: Background service for event consumption
- `OrderProcessingService`: Business logic for order processing

**Dependencies:**
- Azure.Messaging.EventHubs
- Azure.Messaging.EventHubs.Processor
- Azure.Storage.Blobs
- Microsoft.Azure.Cosmos

### 3. Models (`OrderProcessingSystem.Models`)

**Key Models:**
- `Order`: Order entity
- `OrderItem`: Order line item
- `OrderCreatedEvent`: Event payload
- `CreateOrderRequest`: API request model

### 4. Frontend

**Technology:**
- HTML5, CSS3, JavaScript
- Bootstrap 5
- Bootstrap Icons

**Features:**
- Responsive design
- Order creation form
- Order listing
- Order details modal
- Real-time updates

## Error Handling Strategy

### Retry Policy

**Configuration:**
- Max Retries: 3
- Backoff Strategy: Exponential (2^attempt * delay)
- Initial Delay: 2 seconds
- Applies To: Cosmos DB operations, Event Grid publishing

**Implementation:**
```csharp
Policy
    .Handle<Exception>(ex => !(ex is ArgumentException || ex is InvalidOperationException))
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * 2)
    );
```

### Error Types

1. **Transient Errors**
   - Network timeouts
   - Service throttling
   - Temporary unavailability
   - **Action**: Retry with exponential backoff

2. **Permanent Errors**
   - Invalid input data
   - Resource not found
   - Authentication failures
   - **Action**: Return error immediately

3. **Event Processing Errors**
   - Malformed events
   - Processing failures
   - **Action**: Log error, don't update checkpoint (will retry)

## Scalability Considerations

### Horizontal Scaling

- **API**: Stateless, can scale horizontally
- **Worker**: Multiple instances can process different partitions
- **Cosmos DB**: Auto-scaling based on throughput
- **Event Hub**: Partition-based scaling

### Performance Optimization

- Connection pooling for Cosmos DB
- Batch operations where possible
- Async/await throughout
- Efficient partition key strategy

## Security

### Authentication
- APIM subscription keys
- Connection strings stored in App Settings (consider Key Vault)

### Data Protection
- HTTPS only
- Encrypted connections to Azure services
- Secure storage of credentials

## Monitoring & Observability

### Logging
- Application Insights (recommended)
- Console logging for development
- Structured logging

### Metrics
- Request rates
- Error rates
- Processing latency
- Event throughput

## Disaster Recovery

### Backup Strategy
- Cosmos DB automatic backups
- Event Hub message retention
- Blob Storage redundancy

### Failover
- Multi-region deployment (future enhancement)
- Event Hub geo-disaster recovery
- Cosmos DB multi-region writes

## Cost Optimization

### Development
- Use Basic/Free tiers
- Scale down when not in use
- Cosmos DB serverless mode

### Production
- Right-size App Service plans
- Optimize Cosmos DB throughput
- Use appropriate Event Hub tiers

## Future Enhancements

1. **Authentication & Authorization**
   - Azure AD integration
   - Role-based access control

2. **Advanced Features**
   - Order cancellation
   - Payment processing
   - Email notifications
   - Order history

3. **Monitoring**
   - Application Insights integration
   - Custom dashboards
   - Alerting

4. **Performance**
   - Caching layer (Redis)
   - CDN for static assets
   - Database indexing optimization

