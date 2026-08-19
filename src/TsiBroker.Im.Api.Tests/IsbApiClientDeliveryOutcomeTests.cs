using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using Xunit;

namespace TsiBroker.Im.Api.Tests;

// TICKET-6 coverage: spec 2.3.2 step 2 distinguishes ACK/NACK (2a, "erfolgreich abgeschlossen"),
// a response that's neither (2b, discard outright) and no response within DeliveryTimeout (2c,
// resend until max message age) — IsbApiClient.DeliverMessageAsync classifies a delivery attempt
// into a DeliveryOutcome for InfrastructureOperatorConsumerCoordinator to act on accordingly,
// rather than collapsing everything into a plain success/failure bool as it used to.
public class IsbApiClientDeliveryOutcomeTests
{
    [Fact]
    public async Task DeliverMessageAsync_ReceivesAck_ReturnsAcknowledged() =>
        await RunAsync(BuildResponseEnvelope("ACK"), expected: DeliveryOutcome.Acknowledged);

    [Fact]
    public async Task DeliverMessageAsync_ReceivesNack_ReturnsNegativeAcknowledged() =>
        await RunAsync(BuildResponseEnvelope("NACK"), expected: DeliveryOutcome.NegativeAcknowledged);

    [Fact]
    public async Task DeliverMessageAsync_ReceivesUnrelatedResponse_ReturnsInvalidResponse() =>
        await RunAsync("<not-a-uic-response/>", expected: DeliveryOutcome.InvalidResponse);

    [Fact]
    public async Task DeliverMessageAsync_ServerNeverResponds_ReturnsTimedOutWithinDeliveryTimeout()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-delivery-outcome-tests").FullName;
        try
        {
            await using var certificateStoreApp = IsbApiClientCertificateTests.BuildCertificateStoreApp(dataDirectory);

            var imBuilder = WebApplication.CreateBuilder();
            imBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var imApp = imBuilder.Build();
            imApp.MapPost("/ci", async (HttpContext ctx) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ctx.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                }
            });
            await imApp.StartAsync();

            var infrastructureOperator = new InfrastructureOperator
            {
                Id = Guid.NewGuid(),
                Name = "Slow IM",
                RicsCode = "8010000-IM-TIMEOUT",
                SystemUrl = imApp.Urls.First(),
            };

            var sut = new IsbApiClient(
                certificateStoreApp.Services.GetRequiredService<PartnerCertificateProvider>(),
                certificateStoreApp.Services.GetRequiredService<PartnerCertificateValidator>(),
                certificateStoreApp.Services.GetRequiredService<CrlCache>(),
                Options.Create(new InfrastructureOperatorDeliveryOptions { DeliveryTimeout = TimeSpan.FromMilliseconds(150) }),
                NullLogger<IsbApiClient>.Instance);

            var message = new BrokerMessage(
                Id: Guid.NewGuid().ToString(), Sender: "sender", Receiver: "receiver", Content: "<TestMessage/>", CreatedAt: DateTimeOffset.UtcNow);

            var started = DateTimeOffset.UtcNow;
            var outcome = await sut.DeliverMessageAsync(infrastructureOperator, message);
            var elapsed = DateTimeOffset.UtcNow - started;

            Assert.Equal(DeliveryOutcome.TimedOut, outcome);
            // Not a tight bound — just proves it didn't fall back to HttpClient's 100s default.
            Assert.True(elapsed < TimeSpan.FromSeconds(5), $"Expected the 150ms DeliveryTimeout to fire quickly, took {elapsed}.");

            await imApp.StopAsync();
        }
        finally
        {
            try
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static async Task RunAsync(string responseXml, DeliveryOutcome expected)
    {
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-delivery-outcome-tests").FullName;
        try
        {
            await using var certificateStoreApp = IsbApiClientCertificateTests.BuildCertificateStoreApp(dataDirectory);

            var imBuilder = WebApplication.CreateBuilder();
            imBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var imApp = imBuilder.Build();
            imApp.MapPost("/ci", () => Results.Text(responseXml, "text/xml"));
            await imApp.StartAsync();

            var infrastructureOperator = new InfrastructureOperator
            {
                Id = Guid.NewGuid(),
                Name = "Test IM",
                RicsCode = "8010000-IM-OUTCOME",
                SystemUrl = imApp.Urls.First(),
            };

            var sut = new IsbApiClient(
                certificateStoreApp.Services.GetRequiredService<PartnerCertificateProvider>(),
                certificateStoreApp.Services.GetRequiredService<PartnerCertificateValidator>(),
                certificateStoreApp.Services.GetRequiredService<CrlCache>(),
                Options.Create(new InfrastructureOperatorDeliveryOptions()),
                NullLogger<IsbApiClient>.Instance);

            var message = new BrokerMessage(
                Id: Guid.NewGuid().ToString(), Sender: "sender", Receiver: "receiver", Content: "<TestMessage/>", CreatedAt: DateTimeOffset.UtcNow);

            Assert.Equal(expected, await sut.DeliverMessageAsync(infrastructureOperator, message));

            await imApp.StopAsync();
        }
        finally
        {
            try
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static string BuildResponseEnvelope(string responseStatus) =>
        $"""
         <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
           <soap:Body>
             <ns3:UICMessageResponse xmlns:ns2="http://uic.cc.org/UICMessage/Header" xmlns:ns3="http://uic.cc.org/UICMessage">
               <return>
                 <LI_TechnicalAck>
                   <ResponseStatus>{responseStatus}</ResponseStatus>
                 </LI_TechnicalAck>
               </return>
             </ns3:UICMessageResponse>
           </soap:Body>
         </soap:Envelope>
         """;
}
