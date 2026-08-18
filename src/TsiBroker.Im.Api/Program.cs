using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using TsiBroker.Im.Api.CI;
using TsiBroker.Im.Api.Heartbeat;

var builder = WebApplication.CreateBuilder(args);

// TICKET-1: request a client certificate on every HTTPS connection so both 1-way partners (no
// cert offered) and 2-way partners (cert offered) can share this one Kestrel endpoint — a single
// port can't run two different ClientCertificateModes, so the mode is deliberately permissive
// here and per-partner enforcement happens in application code instead (see
// CommonInterfaceMessageService). AllowAnyClientCertificate() is required too: partner CAs are
// per-partner custom roots (PartnerCertificateBundle), never in the machine trust store, so
// Kestrel's default chain validation would otherwise reject every partner cert outright before it
// ever reaches the app. TICKET-2's PartnerCertificateValidator does the real per-partner
// CA/CN/CRL check.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(https =>
    {
        https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        https.AllowAnyClientCertificate();
    });
});

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMessagePublisher(builder.Configuration);

builder.Services
    .AddOptions<InfrastructureOperatorStoreOptions>()
    .Bind(builder.Configuration.GetSection(InfrastructureOperatorStoreOptions.SectionName));
builder.Services.AddSingleton<InfrastructureOperatorStore>();

builder.Services
    .AddOptions<RailwayUndertakingStoreOptions>()
    .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
builder.Services.AddSingleton<RailwayUndertakingStore>();

// PartnerCertificateProvider/PartnerCertificateValidator are used by CommonInterfaceMessageService
// to look up whether the calling partner is provisioned for 2-way SSL (TICKET-1) and, if so, to
// validate the presented client certificate's CA/CN/CRL status (TICKET-2). No admin endpoints or
// expiry monitor in this process, those live in TsiBroker.ApiService.
builder.Services
    .AddOptions<CertificateBundleStoreOptions>()
    .Bind(builder.Configuration.GetSection(CertificateBundleStoreOptions.SectionName));
builder.Services.AddSingleton<CertificateBundleStore>();
builder.Services.AddSingleton<PartnerCertificateProvider>();
// A single long-lived HttpClient is fine here: CrlCache only ever talks to a small, fixed set of
// partner-configured CRL endpoints, not the general "many, changing hosts" scenario
// IHttpClientFactory exists for.
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<CrlCache>();
builder.Services.AddSingleton<PartnerCertificateValidator>();

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

    // BasicHttpBinding(..., BasicHttpSecurityMode.Transport) is correct and sufficient for mTLS
    // here, not just for plain HTTPS: CoreWCF hosted on Kestrel delegates all TLS handling,
    // including client certificates, to Kestrel's HttpsConnectionMiddleware (configured above) —
    // there's no separate WCF-level transport handshake underneath it. WSHttpBinding /
    // ClientCredentialType.Certificate is a different, inapplicable concept here: it configures
    // WCF's own message-level security or classic HTTP.sys hosting, neither of which is in play
    // for CoreWCF-over-ASP.NET-Core.
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
