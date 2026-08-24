using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TsiBroker.ApiService.RailwayUndertakings;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using Xunit;

namespace TsiBroker.ApiService.Tests;

// A processing error on the EVU side (the EVU received the message and responded with a non-2xx
// status, e.g. 400 for malformed XML) must not be treated the same as the EVU being unreachable:
// it should be dead-lettered immediately, without retrying and — critically — without pausing the
// partition's queue. Only a real transport failure (no response at all) reaches the reachability
// check/pause logic. Mirrors InfrastructureOperatorConsumerCoordinatorMaxMessageAgeTests's test
// harness for the opposite (broker -> IM) direction.
public class EvuDeliveryCoordinatorRejectionTests
{
    [Fact]
    public async Task HandleMessageAsync_EvuRejectsMessage_DeadLettersImmediatelyWithoutPausingQueue()
    {
        var healthHitCount = 0;

        var evuBuilder = WebApplication.CreateBuilder();
        evuBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var evuApp = evuBuilder.Build();
        // Simulates infrastructure/evu-endpoints.openapi.yaml's documented 400 response: "Malformed
        // XML or missing required MessageHeader fields" — the EVU actively processed and rejected
        // the message, not a sign it's down.
        evuApp.MapPost("/message", () => Results.StatusCode(StatusCodes.Status400BadRequest));
        // Never called if the fix works: a Rejected outcome must not trigger a reachability check.
        evuApp.MapGet("/health", () =>
        {
            Interlocked.Increment(ref healthHitCount);
            return Results.Ok();
        });
        await evuApp.StartAsync();

        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-apiservice-tests").FullName;
        try
        {
            await using var host = BuildHost(dataDirectory);

            var infrastructureOperatorStore = host.Services.GetRequiredService<InfrastructureOperatorStore>();
            var railwayUndertakingStore = host.Services.GetRequiredService<RailwayUndertakingStore>();

            var infrastructureOperator = await infrastructureOperatorStore.AddAsync(
                "Test IM", "8010000-IM-REJECT", "https://im.example.test");
            var railwayUndertaking = await railwayUndertakingStore.AddAsync(
                "Test RU",
                ["8010000-RU-REJECT"],
                systemUrl: evuApp.Urls.First(),
                [new IsbAssignment { InfrastructureOperatorId = infrastructureOperator.Id, AllowedMessageTypesBrokerToEvu = ["*"] }]);

            var coordinator = host.Services.GetRequiredService<EvuDeliveryCoordinator>();
            var consumer = host.Services.GetRequiredService<CapturingMessageConsumer>();
            var publisher = host.Services.GetRequiredService<CapturingMessagePublisher>();

            await coordinator.StartAsync(railwayUndertaking);
            var handler = consumer.HandlerFor(EvuDeliveryCoordinator.PartitionKeyFor(railwayUndertaking));

            var message = new BrokerMessage(
                Id: Guid.NewGuid().ToString(),
                Sender: "8010000-IM-REJECT",
                Receiver: "8010000-RU-REJECT",
                Content: "<TestMessage/>",
                CreatedAt: DateTimeOffset.UtcNow,
                PartitionKey: EvuDeliveryCoordinator.PartitionKeyFor(railwayUndertaking));

            // Would previously throw MessagePausedException and pause the queue on the first
            // failure — the fix means this returns normally after dead-lettering instead.
            await handler(message, CancellationToken.None);

            var deadLettered = Assert.Single(publisher.DeadLettered);
            Assert.Equal(message.Id, deadLettered.Id);

            Assert.Equal(0, healthHitCount);

            var updated = await railwayUndertakingStore.FindByIdAsync(railwayUndertaking.Id);
            Assert.NotNull(updated);
            Assert.False(updated!.IsQueuePaused);
            Assert.True(consumer.IsPartitionActive(EvuDeliveryCoordinator.PartitionKeyFor(railwayUndertaking)));
        }
        finally
        {
            await evuApp.StopAsync();
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
            ["InfrastructureOperators:DataDirectory"] = dataDirectory,
            ["RailwayUndertakings:DataDirectory"] = dataDirectory,
        });

        builder.Services
            .AddOptions<InfrastructureOperatorStoreOptions>()
            .Bind(builder.Configuration.GetSection(InfrastructureOperatorStoreOptions.SectionName));
        builder.Services.AddSingleton<InfrastructureOperatorStore>();

        builder.Services
            .AddOptions<RailwayUndertakingStoreOptions>()
            .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
        builder.Services.AddSingleton<RailwayUndertakingStore>();

        builder.Services.AddSingleton<EvuMessageAuthorizationService>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<EvuApiClient>();

        builder.Services.AddSingleton<CapturingMessageConsumer>();
        builder.Services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<CapturingMessageConsumer>());
        builder.Services.AddSingleton<CapturingMessagePublisher>();
        builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<CapturingMessagePublisher>());

        builder.Services.AddSingleton<EvuDeliveryCoordinator>();
        builder.Services.AddSingleton<EvuReachabilityMonitor>();
        // Same cycle-breaking as Program.cs — resolving the monitor is deferred until the
        // coordinator actually needs it (.Value), not at construction time.
        builder.Services.AddSingleton(sp => new Lazy<EvuReachabilityMonitor>(sp.GetRequiredService<EvuReachabilityMonitor>));

        return builder.Build();
    }
}
