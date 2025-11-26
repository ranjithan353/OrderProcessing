using System;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Api.Mappings;
using OrderProcessingSystem.Api.Repositories;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Api.Validators;
using OrderProcessingSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// Configure CORS - Allow all origins for API access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Cosmos DB - support local in-memory fallback when connection string is not configured
var cosmosConnectionString = builder.Configuration["CosmosDb:ConnectionString"];
var cosmosDatabaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "OrderProcessingDB";
var cosmosContainerName = builder.Configuration["CosmosDb:ContainerName"] ?? "Orders";

if (!string.IsNullOrWhiteSpace(cosmosConnectionString))
{
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
        var logger = sp.GetRequiredService<ILogger<CosmosDbService>>();
        return new CosmosDbService(cosmosClient, cosmosDatabaseName, cosmosContainerName, logger);
    });
}
else
{
    // Local in-memory fallback for easier local development
    builder.Services.AddSingleton<ICosmosDbService, InMemoryCosmosDbService>();
}

// Configure Event Grid - fallback to no-op when not configured
var eventGridEndpoint = builder.Configuration["EventGrid:TopicEndpoint"];
var eventGridKey = builder.Configuration["EventGrid:TopicKey"];

if (!string.IsNullOrWhiteSpace(eventGridEndpoint) && !string.IsNullOrWhiteSpace(eventGridKey))
{
    builder.Services.AddSingleton<IEventGridService>(sp =>
    {
        var credential = new Azure.AzureKeyCredential(eventGridKey);
        var client = new EventGridPublisherClient(new Uri(eventGridEndpoint), credential);
        var logger = sp.GetRequiredService<ILogger<EventGridService>>();
        return new EventGridService(client, logger);
    });
}
else
{
    builder.Services.AddSingleton<IEventGridService, NoopEventGridService>();
}

// Register Repository
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Register Order Service
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments for easier testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Processing System API V1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Initialize Cosmos DB database and container
var cosmosService = app.Services.GetRequiredService<ICosmosDbService>();
await cosmosService.InitializeAsync();

// Display startup information
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Startup");
logger.LogInformation("");
logger.LogInformation("========================================");
logger.LogInformation("Order Processing System API Started");
logger.LogInformation("========================================");
logger.LogInformation("Swagger UI: http://localhost:5000/swagger");
logger.LogInformation("API Base URL: http://localhost:5000/api/orders");
logger.LogInformation("========================================");
logger.LogInformation("");

app.Run();

