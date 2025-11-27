using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Api.Models;
using Polly;

namespace OrderProcessingSystem.Api.Repositories;

/// <summary>
/// Repository implementation for order data access using Cosmos DB
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(
        ICosmosDbService cosmosDbService,
        ILogger<OrderRepository> logger)
    {
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order> CreateAsync(Order order)
    {
        if (order == null)
        {
            _logger.LogWarning("CreateAsync called with null order");
            throw new ArgumentNullException(nameof(order));
        }

        try
        {
            _logger.LogInformation("Creating order in repository. OrderId: {OrderId}", order.Id);
            
            var createdOrder = await _cosmosDbService.CreateOrderAsync(order);
            
            _logger.LogInformation("Order created successfully in repository. OrderId: {OrderId}", order.Id);
            
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
            _logger.LogError(ex, "Unexpected error creating order in repository. OrderId: {OrderId}", order.Id);
            throw;
        }
    }

    public async Task<Order?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("GetByIdAsync called with null or empty id");
            throw new ArgumentException(Constants.ValidationMessages.OrderIdRequired, nameof(id));
        }

        if (!Guid.TryParse(id, out _))
        {
            _logger.LogWarning("GetByIdAsync called with invalid GUID format. Id: {OrderId}", id);
            throw new ArgumentException(Constants.ValidationMessages.OrderIdInvalidFormat, nameof(id));
        }

        try
        {
            _logger.LogInformation("Retrieving order from repository. OrderId: {OrderId}", id);

            var order = await _cosmosDbService.GetOrderAsync(id);

            if (order == null)
            {
                _logger.LogWarning("Order not found in repository. OrderId: {OrderId}", id);
            }
            else
            {
                _logger.LogInformation("Order retrieved successfully from repository. OrderId: {OrderId}, Status: {Status}",
                    id, order.Status);
            }

            return order;
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
            _logger.LogError(ex, "Unexpected error retrieving order from repository. OrderId: {OrderId}", id);
            throw;
        }
    }

    public async Task<List<Order>> GetAllAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all orders from repository");

            var orders = await _cosmosDbService.GetAllOrdersAsync();

            _logger.LogInformation("Retrieved {Count} orders from repository", orders.Count);

            return orders;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error retrieving all orders. StatusCode: {StatusCode}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving all orders from repository");
            throw;
        }
    }

    public async Task<Order> UpdateStatusAsync(string id, OrderStatus status)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("UpdateStatusAsync called with null or empty id");
            throw new ArgumentException(Constants.ValidationMessages.OrderIdRequired, nameof(id));
        }

        try
        {
            _logger.LogInformation("Updating order status in repository. OrderId: {OrderId}, NewStatus: {Status}", id, status);

            var updatedOrder = await _cosmosDbService.UpdateOrderStatusAsync(id, status);

            _logger.LogInformation("Order status updated successfully in repository. OrderId: {OrderId}, NewStatus: {Status}",
                id, status);

            return updatedOrder;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error updating order status. OrderId: {OrderId}, StatusCode: {StatusCode}",
                id, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating order status in repository. OrderId: {OrderId}", id);
            throw;
        }
    }
}


