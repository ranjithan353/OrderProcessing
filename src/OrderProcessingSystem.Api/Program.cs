using Azure.Messaging.EventGrid;
using Microsoft.Azure.Cosmos;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Cosmos DB
var cosmosConnectionString = builder.Configuration["CosmosDb:ConnectionString"] 
    ?? throw new InvalidOperationException("CosmosDb:ConnectionString is not configured");
var cosmosDatabaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "OrderProcessingDB";
var cosmosContainerName = builder.Configuration["CosmosDb:ContainerName"] ?? "Orders";

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var client = new CosmosClient(cosmosConnectionString, new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
    return client;
});

builder.Services.AddSingleton<ICosmosDbService>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosDbService(cosmosClient, cosmosDatabaseName, cosmosContainerName);
});

// Configure Event Grid
var eventGridEndpoint = builder.Configuration["EventGrid:TopicEndpoint"]
    ?? throw new InvalidOperationException("EventGrid:TopicEndpoint is not configured");
var eventGridKey = builder.Configuration["EventGrid:TopicKey"]
    ?? throw new InvalidOperationException("EventGrid:TopicKey is not configured");

builder.Services.AddSingleton<IEventGridService>(sp =>
{
    var credential = new Azure.AzureKeyCredential(eventGridKey);
    var client = new EventGridPublisherClient(new Uri(eventGridEndpoint), credential);
    var logger = sp.GetRequiredService<ILogger<EventGridService>>();
    return new EventGridService(client, logger);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Initialize Cosmos DB database and container
var cosmosService = app.Services.GetRequiredService<ICosmosDbService>();
await cosmosService.InitializeAsync();

app.Run();

