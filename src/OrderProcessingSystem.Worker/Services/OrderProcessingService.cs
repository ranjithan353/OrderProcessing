using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Worker.Services;

public class OrderProcessingService : IOrderProcessingService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private readonly ILogger<OrderProcessingService> _logger;
    private Container? _container;

    public OrderProcessingService(
        CosmosClient cosmosClient,
        string databaseName,
        string containerName,
        ILogger<OrderProcessingService> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _containerName = containerName;
        _logger = logger;
        InitializeContainer();
    }

    private void InitializeContainer()
    {
        var database = _cosmosClient.GetDatabase(_databaseName);
        _container = database.GetContainer(_containerName);
    }

    public async Task ProcessOrderAsync(string orderId)
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized.");

        try
        {
            _logger.LogInformation("Processing order: {OrderId}", orderId);

            // Simulate processing time
            await Task.Delay(1000);

            // Get the order
            var order = await _container.ReadItemAsync<Order>(
                orderId,
                new PartitionKey(orderId));

            if (order.Resource.Status == OrderStatus.Created)
            {
                // Update order status to Processed
                order.Resource.Status = OrderStatus.Processed;
                order.Resource.ProcessedAt = DateTime.UtcNow;

                await _container.ReplaceItemAsync(
                    order.Resource,
                    orderId,
                    new PartitionKey(orderId));

                _logger.LogInformation("Order processed successfully. OrderId: {OrderId}", orderId);
            }
            else
            {
                _logger.LogWarning("Order {OrderId} is already processed or in invalid state: {Status}", 
                    orderId, order.Resource.Status);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Order not found: {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order: {OrderId}", orderId);
            throw;
        }
    }
}

