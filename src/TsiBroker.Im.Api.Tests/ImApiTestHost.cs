using System.Net;
using System.Security.Cryptography.X509Certificates;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using TsiBroker.Im.Api.CI;
using TsiBroker.Im.Api.Heartbeat;

namespace TsiBroker.Im.Api.Tests;

// Mirrors Program.cs's service registration and CoreWCF endpoint wiring, but bound to an
// OS-assigned loopback port with an explicit throwaway server certificate and the Debug (no
// RabbitMQ) message publisher, so tests can exercise the real Kestrel TLS handshake and
// CommonInterfaceMessageService's client-certificate check without any external dependencies.
internal sealed class ImApiTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _dataDirectory;

    public Uri BaseAddress { get; }
    public InfrastructureOperatorStore InfrastructureOperators => _app.Services.GetRequiredService<InfrastructureOperatorStore>();
    public RailwayUndertakingStore RailwayUndertakings => _app.Services.GetRequiredService<RailwayUndertakingStore>();
    public CertificateBundleStore CertificateBundles => _app.Services.GetRequiredService<CertificateBundleStore>();

    private ImApiTestHost(WebApplication app, Uri baseAddress, string dataDirectory)
    {
        _app = app;
        BaseAddress = baseAddress;
        _dataDirectory = dataDirectory;
    }

    public static async Task<ImApiTestHost> StartAsync(X509Certificate2 serverCertificate)
    {
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-im-api-tests").FullName;

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:QueueType"] = "Debug",
            ["InfrastructureOperators:DataDirectory"] = dataDirectory,
            ["RailwayUndertakings:DataDirectory"] = dataDirectory,
            ["CertificateBundles:DataDirectory"] = dataDirectory,
        });

        // Same ClientCertificateMode.AllowCertificate + AllowAnyClientCertificate() combination as
        // Program.cs (see the comment there for why); the explicit Listen/UseHttps call (instead
        // of ConfigureHttpsDefaults) is test-only plumbing to bind an OS-assigned loopback port
        // with a throwaway certificate.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listenOptions =>
            {
                listenOptions.UseHttps(https =>
                {
                    https.ServerCertificate = serverCertificate;
                    https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                    https.AllowAnyClientCertificate();
                });
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

        builder.Services
            .AddOptions<CertificateBundleStoreOptions>()
            .Bind(builder.Configuration.GetSection(CertificateBundleStoreOptions.SectionName));
        builder.Services.AddSingleton<CertificateBundleStore>();
        builder.Services.AddSingleton<PartnerCertificateProvider>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<CrlCache>();
        builder.Services.AddSingleton<PartnerCertificateValidator>();

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

        await app.StartAsync();

        var addressFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel did not report a bound address.");
        var baseAddress = new Uri(addressFeature.Addresses.First());

        return new ImApiTestHost(app, baseAddress, dataDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        try
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }
}
