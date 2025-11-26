using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Services;

public class InMemoryCosmosDbService : ICosmosDbService
{
    private readonly ConcurrentDictionary<string, Order> _store = new();

    public Task InitializeAsync()
    {
        // Nothing to initialize for in-memory store
        return Task.CompletedTask;
    }

    public Task<Order> CreateOrderAsync(Order order)
    {
        order.Status = OrderStatus.Created;
        order.CreatedAt = DateTime.UtcNow;
        _store[order.Id] = order;
        return Task.FromResult(order);
    }

    public Task<Order?> GetOrderAsync(string id)
    {
        _store.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<List<Order>> GetAllOrdersAsync()
    {
        var list = _store.Values.OrderByDescending(o => o.CreatedAt).ToList();
        return Task.FromResult(list);
    }

    public Task<Order> UpdateOrderStatusAsync(string id, OrderStatus status)
    {
        if (!_store.TryGetValue(id, out var order))
            throw new InvalidOperationException($"Order with id {id} not found.");

        order.Status = status;
        if (status == OrderStatus.Processed)
            order.ProcessedAt = DateTime.UtcNow;

        _store[id] = order;
        return Task.FromResult(order);
    }
}
