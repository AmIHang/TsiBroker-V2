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

builder.Services.AddSingleton<IMessagePublisher, DebugMessagePublisher>();
builder.Services.AddScoped<TsiMessageAuthorizationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapTsiMessageEndpoints();
app.MapWhoAmIEndpoints();

app.MapGet("/", () => "TsiBroker.Ru.Api running");

app.Run();
