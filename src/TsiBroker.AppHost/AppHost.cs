var builder = DistributedApplication.CreateBuilder(args);


var apiService = builder
    .AddProject<Projects.TsiBroker_ApiService>("apiservice")
    .WithExternalHttpEndpoints();

builder
    .AddProject<Projects.TsiBroker_Im_Api>("InfrastructureManagement-api")
    .WithExternalHttpEndpoints();

// Test double for a real ISB: plays both directions of the Common Interface contract
// (receiving on /ci once the outbound relay exists, sending to Im.Api's /ci today). Points at
// Im.Api's static local-dev port by default (see TsiBroker.Im.Mock/appsettings.json) - override
// CiClient__TargetUrl if that port ever stops being fixed.
var isbMock = builder
    .AddProject<Projects.TsiBroker_Im_Mock>("isb-mock")
    .WithExternalHttpEndpoints();

var ruApi = builder
    .AddProject<Projects.TsiBroker_Ru_Api>("RailwayUndertaking-api")
    .WithExternalHttpEndpoints();

// ApiService (admin UI backend) and Ru.Api (EVU-facing) both read/write the same
// flat-file store, so they need to agree on where it lives on disk.
var sharedDataDirectory = Path.Combine(builder.AppHostDirectory, "..", "TsiBroker.ApiService", "App_Data");
apiService.WithEnvironment("RailwayUndertakings__DataDirectory", sharedDataDirectory);
apiService.WithEnvironment("InfrastructureOperators__DataDirectory", sharedDataDirectory);
ruApi.WithEnvironment("RailwayUndertakings__DataDirectory", sharedDataDirectory);
ruApi.WithEnvironment("InfrastructureOperators__DataDirectory", sharedDataDirectory);

var ui = builder
    .AddViteApp("ui", "../TsiBroker.Ui")
    .WithReference(apiService)
    .WithEnvironment(
        "VITE_API_BASE_URL",
        apiService.GetEndpoint("https"))
    .WaitFor(apiService);

apiService.WithEnvironment("Cors__AllowedOrigin", ui.GetEndpoint("http"));

var isbMockUi = builder
    .AddViteApp("isb-mock-ui", "../TsiBroker.Im.Mock.UI")
    .WithReference(isbMock)
    .WithEnvironment(
        "VITE_API_BASE_URL",
        isbMock.GetEndpoint("https"))
    .WaitFor(isbMock);

isbMock.WithEnvironment("Cors__AllowedOrigin", isbMockUi.GetEndpoint("http"));

builder.Build().Run();
