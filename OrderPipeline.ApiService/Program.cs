using System.Text.Json;
using OrderPipeline.ApiService;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddRedisClient("cache");

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running");

app.MapPost("/api/orders", async (OrderRequest request, IConnectionMultiplexer redis, ILogger<Program> logger) =>
{
    var orderEvent = new OrderEvent(Guid.NewGuid().ToString(), request.CustomerId, request.Amount, DateTime.Now);
    var db = redis.GetDatabase();

    await db.PublishAsync(RedisChannel.Literal("orders"), JsonSerializer.Serialize(orderEvent));
    logger.LogInformation("Order {orderId} published to orders channel", orderEvent.OrderId);
    return Results.Accepted($"/orders/{orderEvent.OrderId}", orderEvent);
})
.WithName("CreateOrder");

app.MapDefaultEndpoints();

app.Run();
