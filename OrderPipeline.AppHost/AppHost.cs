var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("orderpipeline-container-app-env");

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.OrderPipeline_ApiService>("apiservice")
    .WithReference(cache)
    .WaitFor(cache)
    .WithExternalHttpEndpoints()
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
