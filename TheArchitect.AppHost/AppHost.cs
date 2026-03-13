var builder = DistributedApplication.CreateBuilder(args);


var qdrant = builder.AddQdrant("qdrant").WithLifetime(ContainerLifetime.Persistent);

var apiService = builder.AddProject<Projects.TheArchitect_ApiService>("apiservice")
    .WithHttpHealthCheck("/health").WithReference(qdrant).WaitFor(qdrant);

builder.AddProject<Projects.TheArchitect_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService).WithReplicas(0);

builder.Build().Run();