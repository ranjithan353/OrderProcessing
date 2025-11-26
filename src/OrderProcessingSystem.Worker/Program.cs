using Azure.Messaging.EventHubs;
using Serilog;
using Microsoft.Extensions.Configuration;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using OrderProcessingSystem.Worker.Services;

// Configure Serilog from configuration
var hostConfig = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(hostConfig)
    .Enrich.FromLogContext()
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, services, configuration) => configuration.ReadFrom.Configuration(hostConfig))
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Configure Event Hub (optional for local dev)
        var eventHubConnectionString = configuration["EventHub:ConnectionString"];
        var eventHubName = configuration["EventHub:Name"] ?? "order-events-hub";
        var consumerGroup = configuration["EventHub:ConsumerGroup"] ?? EventHubConsumerClient.DefaultConsumerGroupName;

        // Configure Blob Storage for checkpointing (optional for local dev)
        var blobStorageConnectionString = configuration["BlobStorage:ConnectionString"];
        var blobContainerName = configuration["BlobStorage:ContainerName"] ?? "eventhub-checkpoints";

        // Configure Cosmos DB (optional for local dev)
        var cosmosConnectionString = configuration["CosmosDb:ConnectionString"];
        var cosmosDatabaseName = configuration["CosmosDb:DatabaseName"] ?? "OrderProcessingDB";
        var cosmosContainerName = configuration["CosmosDb:ContainerName"] ?? "Orders";

        var useEventHub = !string.IsNullOrWhiteSpace(eventHubConnectionString) &&
                          !string.IsNullOrWhiteSpace(blobStorageConnectionString) &&
                          !string.IsNullOrWhiteSpace(cosmosConnectionString);

        if (useEventHub)
        {
            // Register real Azure-backed services
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
        }
        else
        {
            // Local fallback — poll the API over HTTP to find and process created orders
            var apiBase = configuration["Local:ApiBaseUrl"] ?? "https://localhost:7000";
            services.AddHttpClient("api", client => client.BaseAddress = new Uri(apiBase));
            services.AddHostedService<LocalOrderProcessingService>();
        }
    })
    .Build();

await host.RunAsync();

