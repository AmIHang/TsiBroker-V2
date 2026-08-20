using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;

namespace TsiBroker.Im.Api.Tests;

// TICKET-3 coverage needs the reverse of ImApiTestHost: instead of the broker's own /ci endpoint
// enforcing an *inbound* client certificate, this stands in for a partner IM server that requires
// one on the *outbound* connection IsbApiClient makes, and just records whatever certificate the
// caller presented so a test can assert it was the right partner-specific one. RequireCertificate
// (not ImApiTestHost's AllowCertificate) since a real IM server's mandatory 2-way SSL should fail
// the handshake outright when no certificate is presented, not fall through to an app-level check.
internal sealed class ClientCertCapturingServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly CapturedCertificateBox _capturedCertificate;

    public Uri BaseAddress { get; }
    public X509Certificate2? CapturedClientCertificate => _capturedCertificate.Value;

    private ClientCertCapturingServer(WebApplication app, Uri baseAddress, CapturedCertificateBox capturedCertificate)
    {
        _app = app;
        BaseAddress = baseAddress;
        _capturedCertificate = capturedCertificate;
    }

    public static async Task<ClientCertCapturingServer> StartAsync(X509Certificate2 serverCertificate)
    {
        var capturedCertificate = new CapturedCertificateBox();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listenOptions =>
            {
                listenOptions.UseHttps(https =>
                {
                    https.ServerCertificate = serverCertificate;
                    https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    https.ClientCertificateValidation = (certificate, _, _) =>
                    {
                        capturedCertificate.Value = certificate;
                        return true;
                    };
                });
            });
        });

        var app = builder.Build();
        app.MapPost("/ci", () => Results.Text(AckEnvelope, "text/xml"));
        app.MapPost("/heartbeat", () => Results.Ok());

        await app.StartAsync();

        var addressFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel did not report a bound address.");
        var baseAddress = new Uri(addressFeature.Addresses.First());

        return new ClientCertCapturingServer(app, baseAddress, capturedCertificate);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private const string AckEnvelope =
        """<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><Response><ResponseStatus>ACK</ResponseStatus></Response></s:Body></s:Envelope>""";

    // ClientCertificateValidation captures the certificate on Kestrel's TLS callback thread, ahead
    // of anything constructed on the request-handling path — a plain mutable field wrapped in a
    // reference type keeps that write visible to the test's later read without extra ceremony.
    private sealed class CapturedCertificateBox
    {
        public X509Certificate2? Value;
    }
}
