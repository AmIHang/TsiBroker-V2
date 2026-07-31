using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using TsiBroker.Im.Api.CI;
using TsiBroker.Im.Api.Heartbeat;
using TsiBroker.Im.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IMessagePublisher, DebugMessagePublisher>();
builder.Services.AddScoped<CommonInterfaceMessageService>();
builder.Services.AddScoped<HeartbeatMessageService>();

var app = builder.Build();

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
