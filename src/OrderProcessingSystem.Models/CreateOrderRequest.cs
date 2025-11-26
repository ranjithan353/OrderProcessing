using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OrderProcessingSystem.Models;

public class CreateOrderRequest
{
    [Required]
    public string CustomerName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;
    
    [Required]
    public List<OrderItemRequest> Items { get; set; } = new();
    
    [Required]
    public string ShippingAddress { get; set; } = string.Empty;
}

public class OrderItemRequest
{
    [Required]
    public string ProductId { get; set; } = string.Empty;
    
    [Required]
    public string ProductName { get; set; } = string.Empty;
    
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
    
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

