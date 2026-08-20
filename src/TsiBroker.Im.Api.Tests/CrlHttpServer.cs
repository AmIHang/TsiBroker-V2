using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TsiBroker.Im.Api.Tests;

// Serves a CRL over plain HTTP on an OS-assigned loopback port, standing in for a partner's CRL
// distribution point — CrlCache fetches PartnerCertificateBundle.ClientCrlUrl directly (see its
// doc comment for why), so this only needs to be a real, reachable HTTP endpoint, not anything
// tied to the certificate itself. Kestrel rather than HttpListener/http.sys: binding a loopback
// port with Kestrel needs no URL ACL reservation or admin rights, unlike HttpListener — same
// reasoning ImApiTestHost already relies on for its own loopback bind.
//
// The CRL content isn't known until after the certificate it revokes exists (its serial number is
// part of the content), so the server starts serving before SetCrl is called; that's safe because
// nothing fetches the CRL until the test host actually validates a certificate later.
internal sealed class CrlHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly byte[][] _crlBytesBox;

    public Uri CrlUrl { get; }

    private CrlHttpServer(WebApplication app, Uri crlUrl, byte[][] crlBytesBox)
    {
        _app = app;
        CrlUrl = crlUrl;
        _crlBytesBox = crlBytesBox;
    }

    public static async Task<CrlHttpServer> StartAsync()
    {
        var crlBytesBox = new[] { Array.Empty<byte>() };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.MapGet("/crl", async context =>
        {
            context.Response.ContentType = "application/pkix-crl";
            await context.Response.Body.WriteAsync(crlBytesBox[0]);
        });

        await app.StartAsync();

        var addressFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel did not report a bound address.");
        var crlUrl = new Uri(new Uri(addressFeature.Addresses.First()), "/crl");

        return new CrlHttpServer(app, crlUrl, crlBytesBox);
    }

    public void SetCrl(byte[] crlBytes) => _crlBytesBox[0] = crlBytes;

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
