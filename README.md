# Creating an Order Pipeline with Aspire

## Demo

Aspire Dashboard: 

<img width="1637" height="599" alt="image" src="https://github.com/user-attachments/assets/65e88bdc-f31c-47bc-9b6a-4f8b4eccb725" />

Deployed Azure Services:

<img width="805" height="377" alt="image" src="https://github.com/user-attachments/assets/eede090e-283b-40ac-9676-6cc51e0a6018" />

Command and Response:

<img width="1687" height="82" alt="image" src="https://github.com/user-attachments/assets/465b3486-7567-466a-b206-9eec01d0d0c1" />

Trace:

<img width="1634" height="238" alt="image" src="https://github.com/user-attachments/assets/75a9447c-df24-4f6e-b243-3b127a83d148" />

Note that trace doesn't show the subscriber here but you can see the subscriber pick this up in the logs:

<img width="1443" height="60" alt="image" src="https://github.com/user-attachments/assets/b8751381-7038-4090-a2f2-be816143642c" />

## Architecture overview

<img width="744" height="419" alt="image" src="https://github.com/user-attachments/assets/ba271a4d-8f8e-4d9a-9079-e66656c512c3" />

## Setup
We begin by installing aspire and docker desktop. After allowing docker desktop to communicate with WSL we're ready to go

1. Create a new aspire app using the aspire template
```
aspire new aspire-starter -o OrderPipeline
```

2. Aspire will ask if you want to use redis. hit yes

3. Create a worker project for processing:
```
dotnet new worker -o OrderPipeline.Worker
dotnet sln add OrderPipeline.Worker/OrderPipeline.Worker.csproj
```

4. Modify your AppHost.cs it should look like this:

You'll note that it's slightly different to the source code we have in the project. That's ok for now
```
var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.OrderPipeline_ApiService>("apiservice")
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health");

var worker = builder.AddProject<Projects.OrderPipeline_Worker>("worker")
    .WithReference(cache)
    .WaitFor(cache);

builder.AddProject<Projects.OrderPipeline_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(worker)
    .WaitFor(worker);

builder.Build().Run();
```

## Creating the API 

Above we registered the API service with a cache, There's a little bit of groundwork we need to do to actually setup the API 

1. Add the redis cache package so that you can reference it in your project
`Aspire.StackExchange.Redis`

2. Create our basic models for our order endpoint:
```
namespace OrderPipeline.ApiService;

public record OrderRequest(string CustomerId, decimal Amount, List<string> Items);
public record OrderEvent(Guid OrderId, string CustomerId, decimal Amount, DateTime CreatedAt);
```

3. Add the cache to `OrderPipeline.ApiService.Program.cs`
```
builder.AddRedisClient("cache"); // Injected automatically from AppHost
```

Now we can get on to actually creating the API endpoints. You'll see a couple of example endpoints in there and an example record, delete that

1. Add this code to create your endpoint:
```
app.MapPost("/orders", async (OrderRequest request, IConnectionMultiplexer redis, ILogger<Program> logger) =>
{
    var orderEvent = new OrderEvent(Guid.NewGuid(), request.CustomerId, request.Amount, DateTime.UtcNow);
    var db = redis.GetDatabase();

    // Publish to Redis channel
    var payload = System.Text.Json.JsonSerializer.Serialize(orderEvent);
    await db.PublishAsync(RedisChannel.Literal("orders"), payload);

    logger.LogInformation("Order {OrderId} published to processing channel.", orderEvent.OrderId);
    return Results.Accepted($"/orders/{orderEvent.OrderId}", orderEvent);
});
```

## Adding a subscriber/worker

This is a pretty simple step since there's only really 2 files that we need to worry about. Rename the `Worker.cs` to OrderSubscriberWorker if you want to match what I have in here but it doesn't really matter either way. 

`OrderPipeline.Worker/Program.cs`
```
using OrderPipeline.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddRedisClient("cache");
builder.Services.AddHostedService<OrderSubscriberWorker>();

var host = builder.Build();
host.Run();
```

`OrderSubscriberWorker.cs`
```
public class OrderSubscriberWorker(IConnectionMultiplexer redis, ILogger<OrderSubscriberWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var sub = redis.GetSubscriber();

        await sub.SubscribeAsync(RedisChannel.Literal("orders"), (channel, message) =>
        {
            logger.LogInformation("Received order event {message}", message);
        });

        while (!cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(10000, cancellationToken);
        }
    }
}
```

Done

Now you can simply run aspire run and open up a local console
you can use the below cURL command to hit the POST endpoint of your aspire project:
```
 curl -X POST http://localhost:5505/orders   -H "Content-Type: application/json"   -d '{"customerId":"CUST-102","amount":149.99,"items":["W
idget A"]}'
```


## Deploying to Azure

To deploy this to Azure
1. `az login`
2. add this line to your AppHost.cs
```
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("orderpipeline-container-app-env");

.../

```
3. And `WithExternalHttpEndpoints()` to your ApiService registration
4. run `aspire deploy` and let aspire work its magic

