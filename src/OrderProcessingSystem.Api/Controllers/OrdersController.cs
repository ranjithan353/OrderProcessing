using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Api.Validators;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Controllers;

/// <summary>
/// Controller for order management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<CreateOrderRequest> _validator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        IValidator<CreateOrderRequest> validator,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///
    /// <param name="createOrderRequest">The order creation request</param>
    /// <returns>The created order</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Gets an order by ID
    /// </summary>
    /// <param name="id">The order ID</param>
    /// <returns>The order if found</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Gets all orders
    /// </summary>
    /// <returns>List of all orders</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Order>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Updates an order status
    /// </summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> UpdateOrderStatus(string id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            if (request == null)
            {
                _logger.LogWarning("UpdateOrderStatus called with null request");
                return BadRequest(new { error = "Request body is required" });
            }

            _logger.LogInformation("Updating status for OrderId: {OrderId} to {Status}", id, request.Status);

            var updated = await _orderService.UpdateOrderStatusAsync(id, request.Status);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in UpdateOrderStatus for {OrderId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Order with id {id} not found" });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { error = $"Order with id {id} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating order status. OrderId: {OrderId}", id);
            return StatusCode(500, new { error = "An error occurred while updating order status" });
        }
    }
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}
