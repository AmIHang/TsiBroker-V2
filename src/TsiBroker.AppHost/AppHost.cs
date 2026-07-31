var builder = DistributedApplication.CreateBuilder(args);


var apiService = builder
    .AddProject<Projects.TsiBroker_ApiService>("apiservice")
    .WithExternalHttpEndpoints();

builder
    .AddProject<Projects.TsiBroker_Im_Api>("InfrastructureManagement-api")
    .WithExternalHttpEndpoints();


var ui = builder
    .AddViteApp("ui", "../TsiBroker.Ui")
    .WithReference(apiService)
    .WithEnvironment(
        "VITE_API_BASE_URL",
        apiService.GetEndpoint("https"))
    .WaitFor(apiService);

apiService.WithEnvironment("Cors__AllowedOrigin", ui.GetEndpoint("http"));

builder.Build().Run();
