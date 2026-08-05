using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using TsiBroker.Ru.Api.Messages;
using TsiBroker.Ru.Api.WhoAmI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services
    .AddOptions<RailwayUndertakingStoreOptions>()
    .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
builder.Services.AddSingleton<RailwayUndertakingStore>();

builder.Services
    .AddOptions<InfrastructureOperatorStoreOptions>()
    .Bind(builder.Configuration.GetSection(InfrastructureOperatorStoreOptions.SectionName));
builder.Services.AddSingleton<InfrastructureOperatorStore>();

builder.Services.AddMessagePublisher(builder.Configuration);
builder.Services.AddScoped<TsiMessageAuthorizationService>();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapTsiMessageEndpoints();
app.MapWhoAmIEndpoints();

app.MapGet("/", () => "TsiBroker.Ru.Api running");

app.Run();
