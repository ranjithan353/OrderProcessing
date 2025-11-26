using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Services;

public class NoopEventGridService : IEventGridService
{
    private readonly ILogger<NoopEventGridService> _logger;

    public NoopEventGridService(ILogger<NoopEventGridService> logger)
    {
        _logger = logger;
    }

    public Task PublishOrderCreatedEventAsync(OrderCreatedEvent orderEvent)
    {
        _logger.LogInformation("[NoopEventGrid] OrderCreatedEvent would be published: {OrderId}", orderEvent.OrderId);
        return Task.CompletedTask;
    }
}
