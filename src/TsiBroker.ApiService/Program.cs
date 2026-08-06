using Microsoft.AspNetCore.Authentication.Cookies;
using TsiBroker.ApiService.Auth;
using TsiBroker.ApiService.InfrastructureOperators;
using TsiBroker.ApiService.Queues;
using TsiBroker.ApiService.RailwayUndertakings;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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
        options.Cookie.Name = "TsiBroker.Auth";
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
    .AddOptions<InfrastructureOperatorStoreOptions>()
    .Bind(builder.Configuration.GetSection(InfrastructureOperatorStoreOptions.SectionName));
builder.Services.AddSingleton<InfrastructureOperatorStore>();

builder.Services
    .AddOptions<RailwayUndertakingStoreOptions>()
    .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
builder.Services.AddSingleton<RailwayUndertakingStore>();
builder.Services.AddHttpClient<EvuApiClient>();

builder.Services.AddMessageConsumer(builder.Configuration);
builder.Services.AddSingleton<InfrastructureOperatorConsumerCoordinator>();

var app = builder.Build();

// Reconcile: start queue consumption for whatever Infrastrukturbetreiber are already active,
// so a process restart doesn't silently stop delivery until the next Create/Activate/Deactivate call.
{
    var coordinator = app.Services.GetRequiredService<InfrastructureOperatorConsumerCoordinator>();
    var infrastructureOperatorStore = app.Services.GetRequiredService<InfrastructureOperatorStore>();
    try
    {
        await coordinator.StartAllActiveAsync(await infrastructureOperatorStore.GetAllAsync());
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not start message consumption for active infrastructure operators at startup");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(UiCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapInfrastructureOperatorEndpoints();
app.MapRailwayUndertakingEndpoints();
app.MapQueueEndpoints();

app.Run();
