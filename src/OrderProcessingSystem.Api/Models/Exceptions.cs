using System;

namespace OrderProcessingSystem.Api.Models;

/// <summary>
/// Custom exception for order not found scenarios
/// </summary>
public class OrderNotFoundException : Exception
{
    public string OrderId { get; }

    public OrderNotFoundException(string orderId) 
        : base($"Order with id {orderId} not found")
    {
        OrderId = orderId;
    }

    public OrderNotFoundException(string orderId, Exception innerException) 
        : base($"Order with id {orderId} not found", innerException)
    {
        OrderId = orderId;
    }
}

/// <summary>
/// Custom exception for validation failures
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Custom exception for business rule violations
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

