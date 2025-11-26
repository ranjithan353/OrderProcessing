using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Repositories;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Services;

/// <summary>
/// Service layer for order business logic
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventGridService _eventGridService;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IEventGridService eventGridService,
        IMapper mapper,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _eventGridService = eventGridService ?? throw new ArgumentNullException(nameof(eventGridService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

        try
        {
            _logger.LogInformation("Retrieving order. OrderId: {OrderId}", id);

            var order = await _orderRepository.GetByIdAsync(id);

            if (order == null)
            {
                _logger.LogWarning("Order not found. OrderId: {OrderId}", id);
            }
            else
            {
                _logger.LogInformation("Order retrieved successfully. OrderId: {OrderId}, Status: {Status}",
                    id, order.Status);
            }

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order. OrderId: {OrderId}", id);
            throw;
        }
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all orders");

            var orders = await _orderRepository.GetAllAsync();

            _logger.LogInformation("Retrieved {Count} orders", orders.Count);

            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all orders");
            throw;
        }
    }

    public async Task<Order> UpdateOrderStatusAsync(string id, OrderStatus status)
    {
        try
        {
            var updated = await _orderRepository.UpdateStatusAsync(id, status);
            return updated;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status. OrderId: {OrderId}", id);
            throw;
        }
    }
}

