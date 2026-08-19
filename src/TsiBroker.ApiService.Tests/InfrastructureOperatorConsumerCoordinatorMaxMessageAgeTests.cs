using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TsiBroker.ApiService.InfrastructureOperators;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using Xunit;

namespace TsiBroker.ApiService.Tests;

// TICKET-6 coverage: spec 2.3.2 step 2c ("Der BDV-Client empfängt innerhalb von 5 Sekunden keine
// Response. In diesem Fall wird die Nachricht erneut gesendet, bis das maximale Alter der
// Nachricht erreicht ist") — InfrastructureOperatorConsumerCoordinator keeps resending a message
// that only ever times out, but stops and dead-letters it once InfrastructureOperatorDeliveryOptions.
// MaxMessageAge is exceeded, rather than retrying forever or stopping after a fixed attempt count.
public class InfrastructureOperatorConsumerCoordinatorMaxMessageAgeTests
{
    [Fact]
    public async Task HandleMessageAsync_KeepsTimingOut_StopsResendingAndDeadLettersOnceMaxMessageAgeIsReached()
    {
        var ciHitCount = 0;

        var imBuilder = WebApplication.CreateBuilder();
        imBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var imApp = imBuilder.Build();
        // Never responds within the test's DeliveryTimeout — simulates spec 2.3.2 step 2c. Awaits
        // RequestAborted (rather than a fixed delay) so the pending request actually ends once the
        // client's HttpClient.Timeout cancels it, instead of leaking a live request per attempt.
        imApp.MapPost("/ci", async (HttpContext ctx) =>
        {
            Interlocked.Increment(ref ciHitCount);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
            }
        });
        imApp.MapPost("/heartbeat", () => Results.Ok());
        await imApp.StartAsync();

        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-apiservice-tests").FullName;
        try
        {
            await using var host = BuildHost(dataDirectory);

            var infrastructureOperatorStore = host.Services.GetRequiredService<InfrastructureOperatorStore>();
            var railwayUndertakingStore = host.Services.GetRequiredService<RailwayUndertakingStore>();

            var infrastructureOperator = await infrastructureOperatorStore.AddAsync(
                "Test IM", "8010000-IM-MAXAGE", imApp.Urls.First());
            await railwayUndertakingStore.AddAsync(
                "Test RU",
                ["8010000-RU-MAXAGE"],
                systemUrl: "https://ru.example.test",
                [new IsbAssignment { InfrastructureOperatorId = infrastructureOperator.Id, AllowedMessageTypesEvuToBroker = ["*"] }]);

            var coordinator = host.Services.GetRequiredService<InfrastructureOperatorConsumerCoordinator>();
            var consumer = host.Services.GetRequiredService<CapturingMessageConsumer>();
            var publisher = host.Services.GetRequiredService<CapturingMessagePublisher>();

            await coordinator.StartAsync(infrastructureOperator);
            var handler = consumer.HandlerFor(infrastructureOperator.Name);

            var message = new BrokerMessage(
                Id: Guid.NewGuid().ToString(),
                Sender: "8010000-RU-MAXAGE",
                Receiver: infrastructureOperator.RicsCode,
                Content: "<TestMessage/>",
                CreatedAt: DateTimeOffset.UtcNow,
                PartitionKey: infrastructureOperator.Name);

            await handler(message, CancellationToken.None);

            var deadLettered = Assert.Single(publisher.DeadLettered);
            Assert.Equal(message.Id, deadLettered.Id);

            // Bounded by MaxMessageAge / (DeliveryTimeout + RetryDelay) — a handful of attempts,
            // not runaway/unbounded retrying.
            Assert.InRange(ciHitCount, 1, 6);

            var hitCountAtGiveUp = ciHitCount;
            await Task.Delay(TimeSpan.FromMilliseconds(400));
            Assert.Equal(hitCountAtGiveUp, ciHitCount);
        }
        finally
        {
            await imApp.StopAsync();
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

    private static WebApplication BuildHost(string dataDirectory)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CertificateBundles:DataDirectory"] = dataDirectory,
            ["InfrastructureOperators:DataDirectory"] = dataDirectory,
            ["RailwayUndertakings:DataDirectory"] = dataDirectory,
        });

        builder.Services
            .AddOptions<CertificateBundleStoreOptions>()
            .Bind(builder.Configuration.GetSection(CertificateBundleStoreOptions.SectionName));
        builder.Services.AddSingleton<CertificateBundleStore>();
        builder.Services.AddSingleton<PartnerCertificateProvider>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<CrlCache>();
        builder.Services.AddSingleton<PartnerCertificateValidator>();

        builder.Services
            .AddOptions<InfrastructureOperatorStoreOptions>()
            .Bind(builder.Configuration.GetSection(InfrastructureOperatorStoreOptions.SectionName));
        builder.Services.AddSingleton<InfrastructureOperatorStore>();

        builder.Services
            .AddOptions<RailwayUndertakingStoreOptions>()
            .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
        builder.Services.AddSingleton<RailwayUndertakingStore>();

        builder.Services.AddSingleton<IsbMessageAuthorizationService>();

        // Small enough that a message ages out in well under a second, so the test doesn't need
        // to wait out anything close to a real MaxMessageAge.
        builder.Services.Configure<InfrastructureOperatorDeliveryOptions>(options =>
        {
            options.DeliveryTimeout = TimeSpan.FromMilliseconds(150);
            options.RetryDelay = TimeSpan.FromMilliseconds(100);
            options.MaxMessageAge = TimeSpan.FromMilliseconds(500);
        });
        builder.Services.AddSingleton<IsbApiClient>();

        builder.Services.AddSingleton<CapturingMessageConsumer>();
        builder.Services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<CapturingMessageConsumer>());
        builder.Services.AddSingleton<CapturingMessagePublisher>();
        builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<CapturingMessagePublisher>());

        builder.Services.AddSingleton<InfrastructureOperatorConsumerCoordinator>();
        builder.Services.AddSingleton<InfrastructureOperatorReachabilityMonitor>();
        // Same cycle-breaking as Program.cs — resolving the monitor is deferred until a
        // coordinator actually needs it (.Value), not at construction time.
        builder.Services.AddSingleton(sp =>
            new Lazy<InfrastructureOperatorReachabilityMonitor>(sp.GetRequiredService<InfrastructureOperatorReachabilityMonitor>));

        return builder.Build();
    }
}
