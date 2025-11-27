using System.Threading.Tasks;
using OrderProcessingSystem.Api.Models;

namespace OrderProcessingSystem.Api.Services;

public interface IEventGridService
{
    Task PublishOrderCreatedEventAsync(OrderCreatedEvent orderEvent);
}

