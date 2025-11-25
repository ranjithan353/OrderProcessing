using Microsoft.AspNetCore.Mvc;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IEventGridService _eventGridService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        ICosmosDbService cosmosDbService,
        IEventGridService eventGridService,
        ILogger<OrdersController> logger)
    {
        _cosmosDbService = cosmosDbService;
        _eventGridService = eventGridService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            // Calculate total amount
            var totalAmount = request.Items.Sum(item => item.Quantity * item.UnitPrice);

            // Create order
            var order = new Order
            {
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                ShippingAddress = request.ShippingAddress,
                TotalAmount = totalAmount,
                Items = request.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList(),
                Status = "Created"
            };

            // Save to Cosmos DB
            var createdOrder = await _cosmosDbService.CreateOrderAsync(order);
            _logger.LogInformation("Order created successfully. OrderId: {OrderId}", createdOrder.Id);

            // Publish event to Event Grid
            var orderEvent = new OrderCreatedEvent
            {
                OrderId = createdOrder.Id,
                CustomerName = createdOrder.CustomerName,
                CustomerEmail = createdOrder.CustomerEmail,
                TotalAmount = createdOrder.TotalAmount,
                CreatedAt = createdOrder.CreatedAt,
                ShippingAddress = createdOrder.ShippingAddress
            };

            await _eventGridService.PublishOrderCreatedEventAsync(orderEvent);

            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, new { error = "An error occurred while creating the order" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(string id)
    {
        try
        {
            var order = await _cosmosDbService.GetOrderAsync(id);
            if (order == null)
            {
                return NotFound(new { error = $"Order with id {id} not found" });
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order. OrderId: {OrderId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the order" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetAllOrders()
    {
        try
        {
            var orders = await _cosmosDbService.GetAllOrdersAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders");
            return StatusCode(500, new { error = "An error occurred while retrieving orders" });
        }
    }
}

