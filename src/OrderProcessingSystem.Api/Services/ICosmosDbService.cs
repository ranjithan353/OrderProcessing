using OrderProcessingSystem.Api.Models;

namespace OrderProcessingSystem.Api.Services;

public interface ICosmosDbService
{
    Task InitializeAsync();
    Task<Order> CreateOrderAsync(Order order);
    Task<Order?> GetOrderAsync(string id);
    Task<List<Order>> GetAllOrdersAsync();
    Task<Order> UpdateOrderStatusAsync(string id, OrderStatus status);
}

