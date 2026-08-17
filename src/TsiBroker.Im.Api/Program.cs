using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using TsiBroker.Im.Api.CI;
using TsiBroker.Im.Api.Heartbeat;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddMessagePublisher(builder.Configuration);

builder.Services
    .AddOptions<RailwayUndertakingStoreOptions>()
    .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
builder.Services.AddSingleton<RailwayUndertakingStore>();

builder.Services.AddScoped<CommonInterfaceMessageService>();
builder.Services.AddScoped<HeartbeatMessageService>();

var app = builder.Build();

if (app.Services.GetRequiredService<IMessagePublisher>() is RabbitMqMessagePublisher rabbitMqMessagePublisher)
{
    try
    {
        await rabbitMqMessagePublisher.EnsureConnectedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not connect to RabbitMQ at startup");
    }
}

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<CommonInterfaceMessageService>();
    serviceBuilder.AddService<HeartbeatMessageService>();

    serviceBuilder.AddServiceEndpoint<
        CommonInterfaceMessageService,
        ICommonInterfaceMessageService>(
        new BasicHttpBinding(BasicHttpSecurityMode.Transport),
        "/ci");

    serviceBuilder.AddServiceEndpoint<
        HeartbeatMessageService,
        IHeartbeatMessageService>(
        new BasicHttpBinding(BasicHttpSecurityMode.Transport),
        "/heartbeat");
});

var metadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
metadataBehavior.HttpGetEnabled = true;
metadataBehavior.HttpsGetEnabled = true;

app.MapGet("/", () => "TsiBroker.Im.Api running");

app.Run();
