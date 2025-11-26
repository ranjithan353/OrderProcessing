using System;
using System.Threading.Tasks;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest createOrderRequest);
    Task<Order?> GetOrderByIdAsync(string id);
    Task<List<Order>> GetAllOrdersAsync();
    Task<Order> UpdateOrderStatusAsync(string id, OrderStatus status);
}

