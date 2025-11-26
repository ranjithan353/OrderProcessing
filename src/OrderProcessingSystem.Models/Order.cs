using System;
using System.Collections.Generic;

namespace OrderProcessingSystem.Models;

public enum OrderStatus
{
    Created = 0,
    Processing = 1,
    Processed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal => Quantity * UnitPrice;
}

