using Microsoft.AspNetCore.Authentication.Cookies;
using TsiBroker.ApiService.Auth;
using TsiBroker.ApiService.Certificates;
using TsiBroker.ApiService.InfrastructureOperators;
using TsiBroker.ApiService.Queues;
using TsiBroker.ApiService.RailwayUndertakings;
using TsiBroker.Core.Certificates;
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

builder.Services
    .AddOptions<CertificateBundleStoreOptions>()
    .Bind(builder.Configuration.GetSection(CertificateBundleStoreOptions.SectionName));
builder.Services.AddSingleton<CertificateBundleStore>();
builder.Services.AddSingleton<PartnerCertificateProvider>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<CrlCache>();
builder.Services.AddSingleton<PartnerCertificateValidator>();
builder.Services.AddHostedService<CertificateExpiryMonitor>();

// Short timeout so an unreachable EVU is detected in seconds, not the 100s HttpClient default —
// EvuDeliveryCoordinator blocks its one-message-at-a-time partition consumer on this call while
// deciding whether to pause.
builder.Services.AddHttpClient<EvuApiClient>(client => client.Timeout = TimeSpan.FromSeconds(10));

// No AddHttpClient<IsbApiClient> here — each IM partner can have its own client certificate for
// the mandatory 2-way SSL (spec 4.3), so IsbApiClient builds its own HttpClient per outbound call
// instead of using one shared/typed client (see IsbApiClient.CreateHttpClientAsync).
builder.Services.AddSingleton<IsbApiClient>();

builder.Services.AddMessagePublisher(builder.Configuration);
builder.Services.AddMessageConsumer(builder.Configuration);

builder.Services.AddSingleton<IsbMessageAuthorizationService>();
builder.Services.AddSingleton<InfrastructureOperatorConsumerCoordinator>();
builder.Services.AddSingleton<InfrastructureOperatorReachabilityMonitor>();
// Breaks the constructor cycle between InfrastructureOperatorConsumerCoordinator (starts polling
// on failure) and InfrastructureOperatorReachabilityMonitor (restarts the coordinator's partition
// on success) — resolving the monitor is deferred until a coordinator actually needs it (.Value),
// not at construction time.
builder.Services.AddSingleton(sp => new Lazy<InfrastructureOperatorReachabilityMonitor>(sp.GetRequiredService<InfrastructureOperatorReachabilityMonitor>));

builder.Services.AddSingleton<EvuMessageAuthorizationService>();
builder.Services.AddSingleton<EvuDeliveryCoordinator>();
builder.Services.AddSingleton<EvuReachabilityMonitor>();
// Breaks the constructor cycle between EvuDeliveryCoordinator (starts polling on failure) and
// EvuReachabilityMonitor (restarts the coordinator's partition on success) — resolving the
// monitor is deferred until a coordinator actually needs it (.Value), not at construction time.
builder.Services.AddSingleton(sp => new Lazy<EvuReachabilityMonitor>(sp.GetRequiredService<EvuReachabilityMonitor>));

var app = builder.Build();

// Reconcile: start queue consumption for whatever Infrastrukturbetreiber are already active and
// not paused, so a process restart doesn't silently stop delivery until the next
// Create/Activate/Deactivate call, and resume backoff polling (from its persisted
// PauseBackoffStep) for every operator that was still paused when the process last stopped —
// otherwise a restart would silently drop out of the backoff schedule instead of continuing it.
{
    var coordinator = app.Services.GetRequiredService<InfrastructureOperatorConsumerCoordinator>();
    var reachabilityMonitor = app.Services.GetRequiredService<InfrastructureOperatorReachabilityMonitor>();
    var infrastructureOperatorStore = app.Services.GetRequiredService<InfrastructureOperatorStore>();
    try
    {
        var infrastructureOperators = await infrastructureOperatorStore.GetAllAsync();
        await coordinator.StartAllActiveAsync(infrastructureOperators);
        foreach (var infrastructureOperator in infrastructureOperators.Where(io => io.IsQueuePaused))
        {
            reachabilityMonitor.StartPolling(infrastructureOperator);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not start message consumption for active infrastructure operators at startup");
    }
}

// Same reconciliation for EVU (broker -> RU) delivery: start consumption for every active,
// non-paused EVU, and resume backoff polling (from its persisted PauseBackoffStep) for every
// EVU that was still paused when the process last stopped — otherwise a restart would silently
// drop out of the backoff schedule instead of continuing it.
{
    var evuCoordinator = app.Services.GetRequiredService<EvuDeliveryCoordinator>();
    var evuReachabilityMonitor = app.Services.GetRequiredService<EvuReachabilityMonitor>();
    var railwayUndertakingStore = app.Services.GetRequiredService<RailwayUndertakingStore>();
    try
    {
        var railwayUndertakings = await railwayUndertakingStore.GetAllAsync();
        await evuCoordinator.StartAllActiveAsync(railwayUndertakings);
        foreach (var railwayUndertaking in railwayUndertakings.Where(ru => ru.IsQueuePaused))
        {
            evuReachabilityMonitor.StartPolling(railwayUndertaking);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not start EVU message delivery at startup");
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
app.MapCertificateEndpoints();

app.Run();
