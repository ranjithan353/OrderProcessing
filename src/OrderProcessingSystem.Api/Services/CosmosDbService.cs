using Microsoft.Azure.Cosmos;
using OrderProcessingSystem.Models;
using Polly;

namespace OrderProcessingSystem.Api.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private Container? _container;

    public CosmosDbService(CosmosClient cosmosClient, string databaseName, string containerName)
    {
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _containerName = containerName;
    }

    public async Task InitializeAsync()
    {
        var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
        _container = await database.Database.CreateContainerIfNotExistsAsync(
            _containerName,
            "/id",
            400);
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

        var retryPolicy = RetryPolicy.CreateRetryPolicy();
        
        return await retryPolicy.ExecuteAsync(async () =>
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
    }

    public async Task<Order?> GetOrderAsync(string id)
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

        try
        {
            var response = await _container.ReadItemAsync<Order>(
                id,
                new PartitionKey(id));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAt DESC");
        var iterator = _container.GetItemQueryIterator<Order>(query);
        var orders = new List<Order>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            orders.AddRange(response);
        }

        return orders;
    }

    public async Task<Order> UpdateOrderStatusAsync(string id, string status)
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

        var order = await GetOrderAsync(id);
        if (order == null)
            throw new InvalidOperationException($"Order with id {id} not found.");

        order.Status = status;
        if (status == "Processed")
        {
            order.ProcessedAt = DateTime.UtcNow;
        }

        var response = await _container.ReplaceItemAsync(
            order,
            id,
            new PartitionKey(id));

        return response.Resource;
    }
}

