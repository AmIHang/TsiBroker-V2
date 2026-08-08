using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using TsiBroker.Ru.Mock.Auth;
using TsiBroker.Ru.Mock.Diagnostics;
using TsiBroker.Ru.Mock.Messages;
using TsiBroker.Ru.Mock.Receiving;
using TsiBroker.Ru.Mock.Sending;
using TsiBroker.Ru.Mock.Storage;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums (MockMessageDirection) as their names, not numbers, so /api/messages reads
// naturally both for the Vue UI and for anyone poking the API by hand.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddOptions<AdminUserOptions>()
    .Bind(builder.Configuration.GetSection(AdminUserOptions.SectionName));

const string UiCorsPolicy = "ui";
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(UiCorsPolicy, policy =>
    {
        if (allowedOrigin is not null)
        {
            policy.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    });
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TsiBroker.RuMock.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddOptions<MockMessageStoreOptions>()
    .Bind(builder.Configuration.GetSection(MockMessageStoreOptions.SectionName));
builder.Services.AddSingleton<MockMessageStore>();

builder.Services.AddSingleton<ResponseConfigStore>();

builder.Services
    .AddOptions<RuClientOptions>()
    .Bind(builder.Configuration.GetSection(RuClientOptions.SectionName));
builder.Services.AddHttpClient<RuClient>();

builder.Services.AddHostedService<OutboxWatcher>();

var app = builder.Build();

app.Services.GetRequiredService<MockMessageStore>().ResetOnStartup();

app.UseCors(UiCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// Real Broker -> Mock traffic, not a browser - stays unauthenticated. See
// infrastructure/evu-endpoints.openapi.yaml for the documented contract these implement.
app.MapReceiveMessageEndpoints();
app.MapReceiveConfigUpdateEndpoints();
app.MapHealthEndpoints();

app.MapAuthEndpoints();
app.MapResponseConfigEndpoints();
app.MapSendMessageEndpoints();
app.MapMessagesEndpoints();
app.MapWhoAmIEndpoints();

app.Run();
