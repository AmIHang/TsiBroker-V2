using System.Text.Json.Serialization;
using TsiBroker.Im.Mock.Messages;
using TsiBroker.Im.Mock.Receiving;
using TsiBroker.Im.Mock.Sending;
using TsiBroker.Im.Mock.Storage;
using TsiBroker.Im.Mock.Ui;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums (MockMessageDirection) as their names, not numbers, so /api/messages reads
// naturally both for the embedded UI and for anyone poking the API by hand.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddOptions<MockMessageStoreOptions>()
    .Bind(builder.Configuration.GetSection(MockMessageStoreOptions.SectionName));
builder.Services.AddSingleton<MockMessageStore>();

builder.Services.AddSingleton<ResponseConfigStore>();

builder.Services
    .AddOptions<CiClientOptions>()
    .Bind(builder.Configuration.GetSection(CiClientOptions.SectionName));
builder.Services.AddHttpClient<CiClient>();

builder.Services.AddHostedService<OutboxWatcher>();

var app = builder.Build();

app.Services.GetRequiredService<MockMessageStore>().ResetOnStartup();

app.MapReceiveCiEndpoints();
app.MapResponseConfigEndpoints();
app.MapSendCiEndpoints();
app.MapMessagesEndpoints();

app.MapGet("/", () => Results.Content(IndexPage.Html, "text/html"));

app.Run();
