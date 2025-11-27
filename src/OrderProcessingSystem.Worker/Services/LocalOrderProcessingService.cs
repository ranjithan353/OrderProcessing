using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Models;

namespace OrderProcessingSystem.Worker.Services;

public class LocalOrderProcessingService : BackgroundService
{
    private readonly ILogger<LocalOrderProcessingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public LocalOrderProcessingService(IHttpClientFactory httpClientFactory, ILogger<LocalOrderProcessingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("api");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LocalOrderProcessingService started — polling API for created orders.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var orders = await _httpClient.GetFromJsonAsync<List<Order>>("api/orders", cancellationToken: stoppingToken);
                if (orders != null)
                {
                    var created = orders.Where(o => o.Status == OrderStatus.Created).ToList();
                    foreach (var order in created)
                    {
                        _logger.LogInformation("Processing order via API: {OrderId}", order.Id);

                        var req = new { Status = OrderStatus.Processed };
                        var response = await _httpClient.PutAsJsonAsync($"api/orders/{order.Id}/status", req, stoppingToken);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Order processed (via API) successfully: {OrderId}", order.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to process order {OrderId} via API. StatusCode: {Status}", order.Id, response.StatusCode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while polling API for orders");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
