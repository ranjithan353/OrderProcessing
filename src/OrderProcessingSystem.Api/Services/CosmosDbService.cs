using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Models;
using Polly;

namespace OrderProcessingSystem.Api.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private readonly ILogger<CosmosDbService> _logger;
    private Container? _container;

    public CosmosDbService(
        CosmosClient cosmosClient, 
        string databaseName, 
        string containerName,
        ILogger<CosmosDbService> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Cosmos DB database and container. Database: {DatabaseName}, Container: {ContainerName}",
                _databaseName, _containerName);

            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
            _container = await database.Database.CreateContainerIfNotExistsAsync(
                _containerName,
                "/id",
                400);

            _logger.LogInformation("Cosmos DB initialized successfully. Database: {DatabaseName}, Container: {ContainerName}",
                _databaseName, _containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Cosmos DB. Database: {DatabaseName}, Container: {ContainerName}",
                _databaseName, _containerName);
            throw;
        }
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        if (order == null)
        {
            _logger.LogWarning("CreateOrderAsync called with null order");
            throw new ArgumentNullException(nameof(order));
        }

        if (_container == null)
        {
            _logger.LogError("Container not initialized. Call InitializeAsync first.");
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");
        }

        try
        {
            _logger.LogInformation("Creating order in Cosmos DB. OrderId: {OrderId}", order.Id);

            var retryPolicy = RetryPolicy.CreateRetryPolicy();
            
            var createdOrder = await retryPolicy.ExecuteAsync(async () =>
            {
                var response = await _container.CreateItemAsync(
                    order,
                    new PartitionKey(order.Id),
                    new ItemRequestOptions
                    {
                        EnableContentResponseOnWrite = false
                    });

                return order;
            });

            _logger.LogInformation("Order created successfully in Cosmos DB. OrderId: {OrderId}", order.Id);
            return createdOrder;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error creating order. OrderId: {OrderId}, StatusCode: {StatusCode}",
                order.Id, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating order in Cosmos DB. OrderId: {OrderId}", order.Id);
            throw;
        }
    }

    public async Task<Order?> GetOrderAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("GetOrderAsync called with null or empty id");
            throw new ArgumentException("Order ID cannot be null or empty", nameof(id));
        }

        if (_container == null)
        {
            _logger.LogError("Container not initialized. Call InitializeAsync first.");
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");
        }

        try
        {
            _logger.LogInformation("Retrieving order from Cosmos DB. OrderId: {OrderId}", id);

            var response = await _container.ReadItemAsync<Order>(
                id,
                new PartitionKey(id));

            _logger.LogInformation("Order retrieved successfully from Cosmos DB. OrderId: {OrderId}", id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Order not found in Cosmos DB. OrderId: {OrderId}", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error retrieving order. OrderId: {OrderId}, StatusCode: {StatusCode}",
                id, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving order from Cosmos DB. OrderId: {OrderId}", id);
            throw;
        }
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        if (_container == null)
        {
            _logger.LogError("Container not initialized. Call InitializeAsync first.");
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");
        }

        try
        {
            _logger.LogInformation("Retrieving all orders from Cosmos DB");

            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAt DESC");
            var iterator = _container.GetItemQueryIterator<Order>(query);
            var orders = new List<Order>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                orders.AddRange(response);
            }

            _logger.LogInformation("Retrieved {Count} orders from Cosmos DB", orders.Count);
            return orders;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error retrieving all orders. StatusCode: {StatusCode}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving all orders from Cosmos DB");
            throw;
        }
    }

    public async Task<Order> UpdateOrderStatusAsync(string id, OrderStatus status)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("UpdateOrderStatusAsync called with null or empty id");
            throw new ArgumentException("Order ID cannot be null or empty", nameof(id));
        }

        if (_container == null)
        {
            _logger.LogError("Container not initialized. Call InitializeAsync first.");
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");
        }

        try
        {
            _logger.LogInformation("Updating order status in Cosmos DB. OrderId: {OrderId}, NewStatus: {Status}", id, status);

            var order = await GetOrderAsync(id);
            if (order == null)
            {
                _logger.LogWarning("Order not found for status update. OrderId: {OrderId}", id);
                throw new InvalidOperationException($"Order with id {id} not found.");
            }

            order.Status = status;
            if (status == OrderStatus.Processed)
            {
                order.ProcessedAt = DateTime.UtcNow;
            }

            var response = await _container.ReplaceItemAsync(
                order,
                id,
                new PartitionKey(id));

            _logger.LogInformation("Order status updated successfully in Cosmos DB. OrderId: {OrderId}, NewStatus: {Status}",
                id, status);

            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error updating order status. OrderId: {OrderId}, StatusCode: {StatusCode}",
                id, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating order status in Cosmos DB. OrderId: {OrderId}", id);
            throw;
        }
    }
}

