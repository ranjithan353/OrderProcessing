using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using OrderProcessingSystem.Api.Models;

namespace OrderProcessingSystem.Worker.Services;

public class EventHubProcessorService : BackgroundService
{
    private readonly string _eventHubConnectionString;
    private readonly string _eventHubName;
    private readonly string _consumerGroup;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ILogger<EventHubProcessorService> _logger;
    private EventProcessorClient? _processor;

    public EventHubProcessorService(
        string eventHubConnectionString,
        string eventHubName,
        string consumerGroup,
        BlobContainerClient blobContainerClient,
        IOrderProcessingService orderProcessingService,
        ILogger<EventHubProcessorService> logger)
    {
        _eventHubConnectionString = eventHubConnectionString;
        _eventHubName = eventHubName;
        _consumerGroup = consumerGroup;
        _blobContainerClient = blobContainerClient;
        _orderProcessingService = orderProcessingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Hub Processor Service is starting.");

        // Ensure blob container exists for checkpointing
        await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        _processor = new EventProcessorClient(
            _blobContainerClient,
            _consumerGroup,
            _eventHubConnectionString,
            _eventHubName);

        _processor.ProcessEventAsync += ProcessEventHandler;
        _processor.ProcessErrorAsync += ProcessErrorHandler;

        try
        {
            await _processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Event Hub Processor started successfully.");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Event Hub Processor");
            throw;
        }
        finally
        {
            if (_processor != null)
            {
                await _processor.StopProcessingAsync();
                _logger.LogInformation("Event Hub Processor stopped.");
            }
        }
    }

    private async Task ProcessEventHandler(ProcessEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.HasEvent)
            {
                var eventData = eventArgs.Data;
                var eventBody = Encoding.UTF8.GetString(eventData.EventBody.ToArray());
                
                _logger.LogInformation("Received event: {EventBody}", eventBody);

                // Event Grid sends events as an array, but Event Hub might receive them individually
                // Try to parse as array first, then as single event
                List<EventGridEventWrapper> events = new();
                
                try
                {
                    // Try parsing as array
                    var eventArray = JsonSerializer.Deserialize<List<EventGridEventWrapper>>(eventBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (eventArray != null)
                    {
                        events.AddRange(eventArray);
                    }
                }
                catch
                {
                    // If not an array, try as single event
                    var singleEvent = JsonSerializer.Deserialize<EventGridEventWrapper>(eventBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (singleEvent != null)
                    {
                        events.Add(singleEvent);
                    }
                }

                // Process each event
                foreach (var eventGridEvent in events)
                {
                    if (eventGridEvent != null && eventGridEvent.EventType == Constants.EventTypes.OrderCreated)
                    {
                        // Extract order data from the event
                        OrderCreatedEvent? orderData = null;
                        
                        if (eventGridEvent.Data.ValueKind == JsonValueKind.Object)
                        {
                            orderData = JsonSerializer.Deserialize<OrderCreatedEvent>(
                                eventGridEvent.Data.GetRawText(),
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }
                        else if (eventGridEvent.Data.ValueKind == JsonValueKind.String)
                        {
                            orderData = JsonSerializer.Deserialize<OrderCreatedEvent>(
                                eventGridEvent.Data.GetString() ?? "{}",
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }

                        if (orderData != null)
                        {
                            _logger.LogInformation("Processing Order.Created event for OrderId: {OrderId}", orderData.OrderId);
                            await _orderProcessingService.ProcessOrderAsync(orderData.OrderId);
                        }
                    }
                }

                await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event");
            // Don't update checkpoint on error - will retry
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
    {
        _logger.LogError(eventArgs.Exception, "Error in Event Hub Processor. Partition: {PartitionId}", 
            eventArgs.PartitionId);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event Hub Processor Service is stopping.");
        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }
}

// Helper class to deserialize Event Grid events
internal class EventGridEventWrapper
{
    public string? Id { get; set; }
    public string? EventType { get; set; }
    public string? Subject { get; set; }
    public DateTime? EventTime { get; set; }
    public JsonElement Data { get; set; }
}

