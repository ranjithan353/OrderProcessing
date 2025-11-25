using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using OrderProcessingSystem.Worker.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Configure Event Hub
        var eventHubConnectionString = configuration["EventHub:ConnectionString"]
            ?? throw new InvalidOperationException("EventHub:ConnectionString is not configured");
        var eventHubName = configuration["EventHub:Name"] ?? "order-events-hub";
        var consumerGroup = configuration["EventHub:ConsumerGroup"] ?? EventHubConsumerClient.DefaultConsumerGroupName;

        // Configure Blob Storage for checkpointing
        var blobStorageConnectionString = configuration["BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured");
        var blobContainerName = configuration["BlobStorage:ContainerName"] ?? "eventhub-checkpoints";

        // Configure Cosmos DB
        var cosmosConnectionString = configuration["CosmosDb:ConnectionString"]
            ?? throw new InvalidOperationException("CosmosDb:ConnectionString is not configured");
        var cosmosDatabaseName = configuration["CosmosDb:DatabaseName"] ?? "OrderProcessingDB";
        var cosmosContainerName = configuration["CosmosDb:ContainerName"] ?? "Orders";

        services.AddSingleton<CosmosClient>(sp =>
        {
            return new CosmosClient(cosmosConnectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        services.AddSingleton<IOrderProcessingService>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var logger = sp.GetRequiredService<ILogger<OrderProcessingService>>();
            return new OrderProcessingService(cosmosClient, cosmosDatabaseName, cosmosContainerName, logger);
        });

        services.AddSingleton<BlobContainerClient>(sp =>
        {
            var blobServiceClient = new BlobServiceClient(blobStorageConnectionString);
            return blobServiceClient.GetBlobContainerClient(blobContainerName);
        });

        services.AddHostedService<EventHubProcessorService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventHubProcessorService>>();
            var orderProcessingService = sp.GetRequiredService<IOrderProcessingService>();
            var blobContainerClient = sp.GetRequiredService<BlobContainerClient>();

            return new EventHubProcessorService(
                eventHubConnectionString,
                eventHubName,
                consumerGroup,
                blobContainerClient,
                orderProcessingService,
                logger);
        });
    })
    .Build();

await host.RunAsync();

