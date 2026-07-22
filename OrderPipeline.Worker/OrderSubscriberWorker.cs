using System.Text.Json;
using StackExchange.Redis;

namespace OrderPipeline.Worker;

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
