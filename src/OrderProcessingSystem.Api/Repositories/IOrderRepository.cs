using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Repositories;

/// <summary>
/// Repository interface for order data access operations
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Creates a new order in the data store
    /// </summary>
    Task<Order> CreateAsync(Order order);

    /// <summary>
    /// Gets an order by ID
    /// </summary>
    Task<Order?> GetByIdAsync(string id);

    /// <summary>
    /// Gets all orders
    /// </summary>
    Task<List<Order>> GetAllAsync();

    /// <summary>
    /// Updates an order's status
    /// </summary>
    Task<Order> UpdateStatusAsync(string id, OrderStatus status);
}


