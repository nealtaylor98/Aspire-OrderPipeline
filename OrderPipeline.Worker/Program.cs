using OrderPipeline.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddRedisClient("cache");
builder.Services.AddHostedService<OrderSubscriberWorker>();

var host = builder.Build();
host.Run();
