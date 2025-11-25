using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Services;

public interface IEventGridService
{
    Task PublishOrderCreatedEventAsync(OrderCreatedEvent orderEvent);
}

