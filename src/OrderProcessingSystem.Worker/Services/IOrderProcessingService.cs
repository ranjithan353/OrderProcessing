namespace OrderProcessingSystem.Worker.Services;

public interface IOrderProcessingService
{
    Task ProcessOrderAsync(string orderId);
}

