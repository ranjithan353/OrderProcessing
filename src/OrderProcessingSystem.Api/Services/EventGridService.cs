using System;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Models;
using Polly;

namespace OrderProcessingSystem.Api.Services;

public class EventGridService : IEventGridService
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridService> _logger;

    public EventGridService(EventGridPublisherClient client, ILogger<EventGridService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishOrderCreatedEventAsync(OrderCreatedEvent orderEvent)
    {
        if (orderEvent == null)
        {
            _logger.LogWarning("PublishOrderCreatedEventAsync called with null orderEvent");
            throw new ArgumentNullException(nameof(orderEvent));
        }

        if (string.IsNullOrWhiteSpace(orderEvent.OrderId))
        {
            _logger.LogWarning("PublishOrderCreatedEventAsync called with null or empty OrderId");
            throw new ArgumentException("OrderId cannot be null or empty", nameof(orderEvent));
        }

        try
        {
            _logger.LogInformation("Publishing OrderCreatedEvent to Event Grid. OrderId: {OrderId}, EventId: {EventId}",
                orderEvent.OrderId, orderEvent.EventId);

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
                    _logger.LogInformation("OrderCreatedEvent published successfully to Event Grid. OrderId: {OrderId}, EventId: {EventId}",
                        orderEvent.OrderId, orderEvent.EventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish OrderCreatedEvent to Event Grid. OrderId: {OrderId}, EventId: {EventId}",
                        orderEvent.OrderId, orderEvent.EventId);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing OrderCreatedEvent to Event Grid. OrderId: {OrderId}",
                orderEvent.OrderId);
            throw;
        }
    }
}

