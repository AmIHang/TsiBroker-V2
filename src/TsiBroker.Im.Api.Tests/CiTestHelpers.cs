using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace TsiBroker.Im.Api.Tests;

// Shared /ci HTTP plumbing used by both ClientCertificateEnforcementTests (TICKET-1, presence-only)
// and CaCrlValidationTests (TICKET-2, CA/CN/CRL trust).
internal static class CiTestHelpers
{
    public static HttpClient CreateHttpClient(X509Certificate2? clientCertificate)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
        };
        if (clientCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { clientCertificate };
        }

        return new HttpClient(handler);
    }

    public static Task<HttpResponseMessage> PostCiMessageAsync(
        HttpClient httpClient, Uri baseAddress, string senderRicsCode, string recipientRicsCode)
    {
        var envelope = CiEnvelope.Build(senderRicsCode, recipientRicsCode, Guid.NewGuid().ToString());
        var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
        return httpClient.PostAsync(new Uri(baseAddress, "/ci"), content);
    }

    public static async Task<string?> ExtractResponseStatusAsync(HttpResponseMessage response)
    {
        var xml = await response.Content.ReadAsStringAsync();
        var document = XDocument.Parse(xml);
        return document.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResponseStatus")?.Value.Trim();
    }
}
