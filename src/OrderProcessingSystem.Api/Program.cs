using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrderProcessingSystem.Api.Mappings;
using OrderProcessingSystem.Api.Repositories;
using OrderProcessingSystem.Api.Services;
using OrderProcessingSystem.Api.Validators;
using OrderProcessingSystem.Api.Models;
using Serilog;

// Configure Serilog early
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Processing System API",
        Version = "v1",
        Description = "API for managing orders with JWT authentication"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
var issuer = jwtSettings["Issuer"] ?? "OrderProcessingSystem";
var audience = jwtSettings["Audience"] ?? "OrderProcessingSystemUsers";
var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Register Authentication Service
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Initialize Cosmos DB database and container
var cosmosService = app.Services.GetRequiredService<ICosmosDbService>();
await cosmosService.InitializeAsync();

// Display startup information
Log.Information("");
Log.Information("========================================");
Log.Information("Order Processing System API Started");
Log.Information("========================================");
Log.Information("Swagger UI: http://localhost:5000/swagger");
Log.Information("API Base URL: http://localhost:5000/api/orders");
Log.Information("Authentication Endpoint: http://localhost:5000/api/auth/login");
Log.Information("========================================");
Log.Information("");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

