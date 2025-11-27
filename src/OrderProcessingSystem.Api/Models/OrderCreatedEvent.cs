using System;

namespace OrderProcessingSystem.Api.Models;

/// <summary>
/// Event published when an order is created
/// </summary>
public class OrderCreatedEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = Constants.EventTypes.OrderCreated;
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ShippingAddress { get; set; } = string.Empty;
}

