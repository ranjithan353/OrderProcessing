# Order Processing System - Refactoring Changes Documentation

This document details all the critical issues that were fixed and the exact locations where changes were applied.

## Table of Contents
1. [Missing Using Statements](#1-missing-using-statements-critical)
2. [Error Handling](#2-error-handling-high)
3. [Input Validation](#3-input-validation-high)
4. [Exception Handling in GET Methods](#4-exception-handling-in-get-methods-high)
5. [ID Validation](#5-id-validation-medium)
6. [Hard-coded Status Strings](#6-hard-coded-status-strings-medium)
7. [AutoMapper Implementation](#7-automapper-implementation)
8. [Logging for Audit Trails](#8-logging-for-audit-trails)
9. [Separation of Concerns](#9-separation-of-concerns)
10. [Repository Pattern](#10-repository-pattern)
11. [Project Structure](#11-project-structure)

---

## 1. Missing Using Statements (Critical)

### Issue
Missing `using System;` and `using System.Threading.Tasks;` in multiple files.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`
**Lines 1-4:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Api.Validators;
using OrderProcessingSystem.Models;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 1-4:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Repositories;
using OrderProcessingSystem.Models;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/OrderRepository.cs`
**Lines 1-4:**
```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Models;
using Polly;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/InMemoryCosmosDbService.cs`
**Lines 1-5:**
```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderProcessingSystem.Models;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Models/Order.cs`
**Lines 1-2:**
```csharp
using System;
using System.Collections.Generic;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Models/CreateOrderRequest.cs`
**Lines 1-2:**
```csharp
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/RetryPolicy.cs`
**Lines 1-2:**
```csharp
using System;
using Polly;
using Polly.Retry;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/IEventGridService.cs`
**Lines 1-2:**
```csharp
using System.Threading.Tasks;
using OrderProcessingSystem.Models;
```

---

## 2. Error Handling (High)

### Issue
Missing try-catch blocks for async operations.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`
**Lines 44-92 (CreateOrder method):**
```csharp
public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest createOrderRequest)
{
    try
    {
        // Validate input
        if (createOrderRequest == null)
        {
            _logger.LogWarning("CreateOrder called with null request");
            return BadRequest(new { error = Constants.ValidationMessages.OrderRequestRequired });
        }

        // Use FluentValidation for validation
        var validationResult = await _validator.ValidateAsync(createOrderRequest);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("CreateOrder called with invalid request. Errors: {Errors}",
                string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
            
            var errors = validationResult.Errors.Select(e => new { 
                field = e.PropertyName, 
                message = e.ErrorMessage 
            });
            
            return BadRequest(new { errors });
        }

        _logger.LogInformation("Creating order for customer: {CustomerName}", createOrderRequest.CustomerName);

        // Delegate to service layer - all business logic is in the service
        var order = await _orderService.CreateOrderAsync(createOrderRequest);

        _logger.LogInformation("Order created successfully. OrderId: {OrderId}", order.Id);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
    catch (Models.ValidationException ex)
    {
        _logger.LogWarning(ex, "Validation error in CreateOrder");
        return BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning(ex, "Invalid argument in CreateOrder");
        return BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error creating order");
        return StatusCode(500, new { error = "An error occurred while creating the order. Please try again later." });
    }
}
```

**Lines 105-146 (GetOrder method):**
```csharp
public async Task<ActionResult<Order>> GetOrder(string id)
{
    try
    {
        // Validate ID parameter
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("GetOrder called with null or empty id");
            return BadRequest(new { error = Constants.ValidationMessages.OrderIdRequired });
        }

        if (!Guid.TryParse(id, out _))
        {
            _logger.LogWarning("GetOrder called with invalid GUID format. Id: {OrderId}", id);
            return BadRequest(new { error = Constants.ValidationMessages.OrderIdInvalidFormat });
        }

        _logger.LogInformation("Retrieving order. OrderId: {OrderId}", id);

        // Delegate to service layer
        var order = await _orderService.GetOrderByIdAsync(id);

        if (order == null)
        {
            _logger.LogWarning("Order not found. OrderId: {OrderId}", id);
            return NotFound(new { error = $"Order with id {id} not found" });
        }

        _logger.LogInformation("Order retrieved successfully. OrderId: {OrderId}", id);

        return Ok(order);
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning(ex, "Invalid argument in GetOrder. OrderId: {OrderId}", id);
        return BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error retrieving order. OrderId: {OrderId}", id);
        return StatusCode(500, new { error = "An error occurred while retrieving the order. Please try again later." });
    }
}
```

**Lines 156-172 (GetAllOrders method):**
```csharp
public async Task<ActionResult<List<Order>>> GetAllOrders()
{
    try
    {
        _logger.LogInformation("Retrieving all orders");

        var orders = await _orderService.GetAllOrdersAsync();

        _logger.LogInformation("Retrieved {Count} orders", orders.Count);

        return Ok(orders);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error retrieving all orders");
        return StatusCode(500, new { error = "An error occurred while retrieving orders. Please try again later." });
    }
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 34-93 (CreateOrderAsync method):**
```csharp
public async Task<Order> CreateOrderAsync(CreateOrderRequest createOrderRequest)
{
    if (createOrderRequest == null)
    {
        _logger.LogWarning("CreateOrderAsync called with null request");
        throw new ArgumentNullException(nameof(createOrderRequest));
    }

    try
    {
        _logger.LogInformation("Creating new order for customer: {CustomerName}, Email: {CustomerEmail}",
            createOrderRequest.CustomerName, createOrderRequest.CustomerEmail);

        // Validate business rules
        ValidateOrderRequest(createOrderRequest);

        // Map DTO to Order entity using AutoMapper
        var order = _mapper.Map<Order>(createOrderRequest);

        // Ensure order status is set correctly
        order.Status = OrderStatus.Created;
        order.CreatedAt = DateTime.UtcNow;

        // Calculate total amount
        order.TotalAmount = createOrderRequest.Items.Sum(item => item.Quantity * item.UnitPrice);

        // Save order using repository
        var createdOrder = await _orderRepository.CreateAsync(order);
        
        _logger.LogInformation("Order created successfully. OrderId: {OrderId}, TotalAmount: {TotalAmount}",
            createdOrder.Id, createdOrder.TotalAmount);

        // Map Order to OrderCreatedEvent and publish to Event Grid
        var orderEvent = _mapper.Map<OrderCreatedEvent>(createdOrder);
        
        try
        {
            await _eventGridService.PublishOrderCreatedEventAsync(orderEvent);
            _logger.LogInformation("OrderCreatedEvent published successfully. OrderId: {OrderId}", createdOrder.Id);
        }
        catch (Exception ex)
        {
            // Log but don't fail the order creation if event publishing fails
            _logger.LogError(ex, "Failed to publish OrderCreatedEvent. OrderId: {OrderId}. Order was still created successfully.", 
                createdOrder.Id);
        }

        return createdOrder;
    }
    catch (Models.ValidationException)
    {
        // Re-throw validation exceptions as-is
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating order for customer: {CustomerName}", 
            createOrderRequest.CustomerName);
        throw;
    }
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/OrderRepository.cs`
**Lines 29-57 (CreateAsync method):**
```csharp
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
```

---

## 3. Input Validation (High)

### Issue
No validation for `createOrderDto` parameter.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Validators/CreateOrderRequestValidator.cs` (NEW FILE)
**Complete file:**
```csharp
using System;
using System.Linq;
using FluentValidation;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Validators;

/// <summary>
/// Validator for CreateOrderRequest using FluentValidation
/// </summary>
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .WithMessage("Customer name is required")
            .MaximumLength(200)
            .WithMessage("Customer name cannot exceed 200 characters");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .WithMessage("Customer email is required")
            .EmailAddress()
            .WithMessage("Customer email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("Customer email cannot exceed 255 characters");

        RuleFor(x => x.ShippingAddress)
            .NotEmpty()
            .WithMessage("Shipping address is required")
            .MaximumLength(500)
            .WithMessage("Shipping address cannot exceed 500 characters");

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("Order items are required")
            .Must(items => items != null && items.Any())
            .WithMessage("Order must contain at least one item");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemRequestValidator());
    }
}

/// <summary>
/// Validator for OrderItemRequest
/// </summary>
public class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required")
            .MaximumLength(100)
            .WithMessage("Product ID cannot exceed 100 characters");

        RuleFor(x => x.ProductName)
            .NotEmpty()
            .WithMessage("Product name is required")
            .MaximumLength(200)
            .WithMessage("Product name cannot exceed 200 characters");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("Quantity cannot exceed 10000");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .WithMessage("Unit price must be greater than 0")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Unit price cannot exceed 1,000,000");
    }
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Program.cs`
**Lines 22-23:**
```csharp
// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`
**Lines 21, 26, 54-66:**
```csharp
private readonly IValidator<CreateOrderRequest> _validator;

public OrdersController(
    IOrderService orderService,
    IValidator<CreateOrderRequest> validator,
    ILogger<OrdersController> logger)
{
    // ...
}

// In CreateOrder method:
// Use FluentValidation for validation
var validationResult = await _validator.ValidateAsync(createOrderRequest);
if (!validationResult.IsValid)
{
    _logger.LogWarning("CreateOrder called with invalid request. Errors: {Errors}",
        string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
    
    var errors = validationResult.Errors.Select(e => new { 
        field = e.PropertyName, 
        message = e.ErrorMessage 
    });
    
    return BadRequest(new { errors });
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 96-120 (ValidateOrderRequest method):**
```csharp
private void ValidateOrderRequest(CreateOrderRequest request)
{
    if (request.Items == null || !request.Items.Any())
    {
        _logger.LogWarning("CreateOrderAsync called with no items");
        throw new Models.ValidationException(Constants.ValidationMessages.ItemsRequired);
    }

    foreach (var item in request.Items)
    {
        if (item.Quantity <= 0)
        {
            _logger.LogWarning("CreateOrderAsync called with invalid quantity: {Quantity} for product: {ProductName}", 
                item.Quantity, item.ProductName);
            throw new Models.ValidationException(string.Format(Constants.ValidationMessages.QuantityInvalid, item.ProductName));
        }

        if (item.UnitPrice <= 0)
        {
            _logger.LogWarning("CreateOrderAsync called with invalid unit price: {UnitPrice} for product: {ProductName}", 
                item.UnitPrice, item.ProductName);
            throw new Models.ValidationException(string.Format(Constants.ValidationMessages.UnitPriceInvalid, item.ProductName));
        }
    }
}
```

---

## 4. Exception Handling in GET Methods (High)

### Issue
Repository operations lack error handling.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/OrderRepository.cs`
**Lines 60-107 (GetByIdAsync method):**
```csharp
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
```

**Lines 110-131 (GetAllAsync method):**
```csharp
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
```

---

## 5. ID Validation (Medium)

### Issue
GetOrder method doesn't validate id parameter.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`
**Lines 109-120:**
```csharp
// Validate ID parameter
if (string.IsNullOrWhiteSpace(id))
{
    _logger.LogWarning("GetOrder called with null or empty id");
    return BadRequest(new { error = Constants.ValidationMessages.OrderIdRequired });
}

if (!Guid.TryParse(id, out _))
{
    _logger.LogWarning("GetOrder called with invalid GUID format. Id: {OrderId}", id);
    return BadRequest(new { error = Constants.ValidationMessages.OrderIdInvalidFormat });
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 122-134:**
```csharp
public async Task<Order?> GetOrderByIdAsync(string id)
{
    if (string.IsNullOrWhiteSpace(id))
    {
        _logger.LogWarning("GetOrderByIdAsync called with null or empty id");
        throw new ArgumentException(Constants.ValidationMessages.OrderIdRequired, nameof(id));
    }

    if (!Guid.TryParse(id, out _))
    {
        _logger.LogWarning("GetOrderByIdAsync called with invalid GUID format. Id: {OrderId}", id);
        throw new ArgumentException(Constants.ValidationMessages.OrderIdInvalidFormat, nameof(id));
    }
    // ...
}
```

---

## 6. Hard-coded Status Strings (Medium)

### Issue
"Created" status should be a constant/Enum.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Models/Order.cs`
**Lines 6-14 (OrderStatus Enum):**
```csharp
public enum OrderStatus
{
    Created = 0,
    Processing = 1,
    Processed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}
```

**Line 23:**
```csharp
public OrderStatus Status { get; set; } = OrderStatus.Created;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Models/Constants.cs` (NEW FILE)
**Lines 11-18:**
```csharp
/// <summary>
/// Event type constants
/// </summary>
public static class EventTypes
{
    public const string OrderCreated = "Order.Created";
    public const string OrderProcessed = "Order.Processed";
    public const string OrderShipped = "Order.Shipped";
    public const string OrderDelivered = "Order.Delivered";
    public const string OrderCancelled = "Order.Cancelled";
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Mappings/OrderMappingProfile.cs`
**Line 17:**
```csharp
.ForMember(dest => dest.Status, opt => opt.MapFrom(src => OrderStatus.Created))
```

**Line 32:**
```csharp
.ForMember(dest => dest.EventType, opt => opt.MapFrom(src => Constants.EventTypes.OrderCreated));
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Models/OrderCreatedEvent.cs`
**Line 11:**
```csharp
public string EventType { get; set; } = Constants.EventTypes.OrderCreated;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Line 54:**
```csharp
order.Status = OrderStatus.Created;
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/InMemoryCosmosDbService.cs`
**Line 18:**
```csharp
order.Status = OrderStatus.Created;
```

---

## 7. AutoMapper Implementation

### Issue
Need to use AutoMapper for object mapping.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Mappings/OrderMappingProfile.cs`
**Complete file:**
```csharp
using System;
using System.Linq;
using AutoMapper;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Mappings;

/// <summary>
/// AutoMapper profile for order-related mappings
/// </summary>
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<CreateOrderRequest, Order>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid().ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => OrderStatus.Created))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => 
                src.Items != null ? src.Items.Sum(item => item.Quantity * item.UnitPrice) : 0))
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => 
                src.Items != null ? src.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList() : new System.Collections.Generic.List<OrderItem>()));

        CreateMap<Order, OrderCreatedEvent>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.EventType, opt => opt.MapFrom(src => Constants.EventTypes.OrderCreated));
    }
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Program.cs`
**Lines 19-20:**
```csharp
// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 19, 30, 51, 67:**
```csharp
private readonly IMapper _mapper;

public OrderService(
    IOrderRepository orderRepository,
    IEventGridService eventGridService,
    IMapper mapper,
    ILogger<OrderService> logger)
{
    // ...
}

// Usage:
var order = _mapper.Map<Order>(createOrderRequest);
var orderEvent = _mapper.Map<OrderCreatedEvent>(createdOrder);
```

---

## 8. Logging for Audit Trails

### Issue
Need to add logging for audit trails.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`
**Lines 22, 31, 50, 58, 69, 74, 80, 85, 90, 112, 118, 122, 129, 133, 139, 144, 160, 169:**
```csharp
private readonly ILogger<OrdersController> _logger;

// Example logging statements:
_logger.LogWarning("CreateOrder called with null request");
_logger.LogInformation("Creating order for customer: {CustomerName}", createOrderRequest.CustomerName);
_logger.LogInformation("Order created successfully. OrderId: {OrderId}", order.Id);
_logger.LogError(ex, "Unexpected error creating order");
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 20, 31, 38, 44, 63, 72, 77, 90, 100, 108, 115, 126, 138, 144, 148, 156, 165, 175:**
```csharp
private readonly ILogger<OrderService> _logger;

// Example logging statements:
_logger.LogInformation("Creating new order for customer: {CustomerName}, Email: {CustomerEmail}",
    createOrderRequest.CustomerName, createOrderRequest.CustomerEmail);
_logger.LogInformation("Order created successfully. OrderId: {OrderId}, TotalAmount: {TotalAmount}",
    createdOrder.Id, createdOrder.TotalAmount);
_logger.LogError(ex, "Error creating order for customer: {CustomerName}", 
    createOrderRequest.CustomerName);
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/OrderRepository.cs`
**Lines 19, 26, 33, 39, 43, 49, 55, 64, 70, 76, 82, 86, 94, 99, 105, 114, 118, 124, 129, 138, 144, 148, 155, 161:**
```csharp
private readonly ILogger<OrderRepository> _logger;

// Example logging statements:
_logger.LogInformation("Creating order in repository. OrderId: {OrderId}", order.Id);
_logger.LogError(ex, "Cosmos DB error creating order. OrderId: {OrderId}, StatusCode: {StatusCode}",
    order.Id, ex.StatusCode);
```

---

## 9. Separation of Concerns

### Issue
Controller doing business logic - Creating orders, mapping objects, orchestrating operations.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`
**Lines 69-72 (Before: Business logic in controller, After: Delegated to service):**
```csharp
// BEFORE (removed):
// var order = new Order { ... }; // Business logic
// order.TotalAmount = calculateTotal(); // Business logic
// await _repository.CreateAsync(order); // Direct repository access

// AFTER:
_logger.LogInformation("Creating order for customer: {CustomerName}", createOrderRequest.CustomerName);

// Delegate to service layer - all business logic is in the service
var order = await _orderService.CreateOrderAsync(createOrderRequest);

_logger.LogInformation("Order created successfully. OrderId: {OrderId}", order.Id);
```

**Lines 122-125:**
```csharp
_logger.LogInformation("Retrieving order. OrderId: {OrderId}", id);

// Delegate to service layer
var order = await _orderService.GetOrderByIdAsync(id);
```

**Lines 160-162:**
```csharp
_logger.LogInformation("Retrieving all orders");

var orders = await _orderService.GetAllOrdersAsync();
```

---

## 10. Repository Pattern

### Issue
Direct repository access - Controllers should not know about data layer.

### Files Changed

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/IOrderRepository.cs` (NEW FILE)
**Complete file:**
```csharp
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
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/OrderRepository.cs` (NEW FILE)
**Lines 1-27:**
```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Models;
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
    // ... implementation methods
}
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`
**Lines 17, 23, 28, 61:**
```csharp
// BEFORE: Direct CosmosDbService access
// private readonly ICosmosDbService _cosmosDbService;

// AFTER: Repository pattern
private readonly IOrderRepository _orderRepository;

public OrderService(
    IOrderRepository orderRepository,  // Changed from ICosmosDbService
    IEventGridService eventGridService,
    IMapper mapper,
    ILogger<OrderService> logger)
{
    _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    // ...
}

// Usage:
var createdOrder = await _orderRepository.CreateAsync(order);
var order = await _orderRepository.GetByIdAsync(id);
var orders = await _orderRepository.GetAllAsync();
```

#### `OrderProcessingSystem/src/OrderProcessingSystem.Api/Program.cs`
**Lines 75-76:**
```csharp
// Register Repository
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Register Order Service
builder.Services.AddScoped<IOrderService, OrderService>();
```

---

## 11. Project Structure

### New Files Created

1. **`OrderProcessingSystem/src/OrderProcessingSystem.Models/Constants.cs`**
   - Contains all magic strings and constants
   - EventTypes, ValidationMessages, Defaults

2. **`OrderProcessingSystem/src/OrderProcessingSystem.Models/Exceptions.cs`**
   - Custom exceptions: `OrderNotFoundException`, `ValidationException`, `BusinessRuleException`

3. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/IOrderRepository.cs`**
   - Repository interface for data access abstraction

4. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Repositories/OrderRepository.cs`**
   - Repository implementation

5. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Validators/CreateOrderRequestValidator.cs`**
   - FluentValidation validators for input validation

### Updated Files

1. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Controllers/OrdersController.cs`**
   - Removed business logic
   - Added comprehensive error handling
   - Added input validation using FluentValidation
   - Added logging throughout

2. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Services/OrderService.cs`**
   - Uses repository pattern instead of direct CosmosDbService
   - Contains all business logic
   - Comprehensive error handling and logging

3. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Program.cs`**
   - Registered FluentValidation
   - Registered Repository
   - Registered AutoMapper
   - Removed Serilog dependency

4. **`OrderProcessingSystem/src/OrderProcessingSystem.Api/Mappings/OrderMappingProfile.cs`**
   - Enhanced with null checks
   - Uses constants for event types

5. **`OrderProcessingSystem/src/OrderProcessingSystem.Models/OrderCreatedEvent.cs`**
   - Uses constants for event types

### Final Architecture

```
OrderProcessingSystem.Api/
├── Controllers/          (HTTP handling only)
│   └── OrdersController.cs
├── Services/            (Business logic)
│   ├── OrderService.cs
│   ├── CosmosDbService.cs
│   ├── EventGridService.cs
│   ├── InMemoryCosmosDbService.cs
│   └── NoopEventGridService.cs
├── Repositories/        (Data access abstraction)
│   ├── IOrderRepository.cs
│   └── OrderRepository.cs
├── Validators/          (Input validation)
│   └── CreateOrderRequestValidator.cs
└── Mappings/           (AutoMapper profiles)
    └── OrderMappingProfile.cs

OrderProcessingSystem.Models/
├── Order.cs
├── CreateOrderRequest.cs
├── OrderCreatedEvent.cs
├── Constants.cs         (NEW)
└── Exceptions.cs        (NEW)
```

---

## Summary

All critical issues have been resolved:

✅ **Missing Using Statements** - Fixed in all files  
✅ **Error Handling** - Comprehensive try-catch blocks added  
✅ **Input Validation** - FluentValidation implemented  
✅ **Exception Handling in GET Methods** - Added to all repository methods  
✅ **ID Validation** - GUID validation added  
✅ **Hard-coded Status Strings** - Replaced with `OrderStatus` enum and constants  
✅ **AutoMapper** - Implemented for all object mappings  
✅ **Logging** - Added throughout all layers  
✅ **Separation of Concerns** - Business logic moved to service layer  
✅ **Repository Pattern** - Implemented to abstract data access  
✅ **Testability** - Improved through dependency injection and interfaces  

The project now follows clean architecture principles with proper separation of concerns, making it maintainable, testable, and scalable.

