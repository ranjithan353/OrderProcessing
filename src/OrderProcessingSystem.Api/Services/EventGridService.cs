using Azure;
using Azure.Messaging.EventGrid;
using OrderProcessingSystem.Models;
using Polly;

namespace OrderProcessingSystem.Api.Services;

public class EventGridService : IEventGridService
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridService> _logger;

    public EventGridService(EventGridPublisherClient client, ILogger<EventGridService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishOrderCreatedEventAsync(OrderCreatedEvent orderEvent)
    {
        var retryPolicy = RetryPolicy.CreateRetryPolicy(maxRetries: 3, delaySeconds: 1);
        
        await retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                var eventData = new EventGridEvent(
                    subject: $"orders/{orderEvent.OrderId}",
                    eventType: orderEvent.EventType,
                    dataVersion: "1.0",
                    data: new BinaryData(orderEvent));

                await _client.SendEventAsync(eventData);
                _logger.LogInformation("Order created event published successfully. OrderId: {OrderId}", orderEvent.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish order created event. OrderId: {OrderId}", orderEvent.OrderId);
                throw;
            }
        });
    }
}

