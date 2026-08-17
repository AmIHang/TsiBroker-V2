# Backend Best Practices

**TsiBroker Development Standards**

These conventions are the backend counterpart to [[Architecture]] — read that first for the "what," this page is the "how." They're also summarized in [AGENTS.md](../AGENTS.md) at the repo root; this page goes into more depth with examples from the actual codebase.

## Table of Contents

1. [Feature Folders, Not Layers](#1-feature-folders-not-layers)
2. [Minimal APIs Only](#2-minimal-apis-only)
3. [No Repository / DbContext Pattern](#3-no-repository--dbcontext-pattern)
4. [No Multi-Tenancy](#4-no-multi-tenancy)
5. [Messaging: Always Through IMessagePublisher](#5-messaging-always-through-imessagepublisher)
6. [SOAP Contract Code Stays Generated-Code-Adjacent](#6-soap-contract-code-stays-generated-code-adjacent)
7. [Validation and Error Responses](#7-validation-and-error-responses)
8. [Options Pattern for Configuration](#8-options-pattern-for-configuration)

---

## 1. Feature Folders, Not Layers

Each feature lives together: entity/store in `TsiBroker.Core`, endpoint mapping in the hosting project. There is no `Controllers/`, `Services/`, `DTOs/` split.

```
TsiBroker.Core/RailwayUndertakings/
├── RailwayUndertaking.cs          # entity + IsbAssignment
└── RailwayUndertakingStore.cs     # persistence

TsiBroker.ApiService/RailwayUndertakings/
└── RailwayUndertakingEndpoints.cs # request/response records + Map...Endpoints
```

Request/response DTOs are plain `record`s declared right next to the endpoint mapping method that uses them (see `CreateRailwayUndertakingRequest`, `UpdateRailwayUndertakingRequest` in `RailwayUndertakingEndpoints.cs`) — don't extract them into a shared `Contracts`/`Dtos` project unless a genuine cross-project reuse need shows up.

## 2. Minimal APIs Only

No MVC controllers, no `[ApiController]`. Every feature exposes a `public static class XyzEndpoints` with a `MapXyzEndpoints(this IEndpointRouteBuilder app)` extension method, called once from `Program.cs`:

```csharp
public static class InfrastructureOperatorEndpoints
{
    public static void MapInfrastructureOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/infrastructure-operators").RequireAuthorization();
        group.MapGet("/", ...);
        group.MapPost("/", ...);
        // ...
    }
}
```

Group-level `.RequireAuthorization()` is used for whole feature groups rather than attribute-based per-endpoint checks.

## 3. No Repository / DbContext Pattern

There's no database, so this isn't "use DbContext directly" — it's "there's no ORM at all." Stores (`InfrastructureOperatorStore`, `RailwayUndertakingStore`) are plain singleton classes over JSON files, registered directly:

```csharp
builder.Services
    .AddOptions<RailwayUndertakingStoreOptions>()
    .Bind(builder.Configuration.GetSection(RailwayUndertakingStoreOptions.SectionName));
builder.Services.AddSingleton<RailwayUndertakingStore>();
```

Endpoints depend on the store class directly (constructor/parameter injection), not an interface — there's exactly one implementation and no need to mock a database in tests, since there's no database. See [[Architecture]] for the locking/concurrency model these stores use.

Cross-store consistency (e.g. removing dangling `IsbAssignment`s when an `InfrastructureOperator` is deleted) is handled explicitly at the endpoint layer, not hidden behind a unit-of-work abstraction — keep it that way; don't introduce a generic "repository" or "unit of work" layer for two JSON files.

## 4. No Multi-Tenancy

TsiBroker is a single-tenant broker. Don't introduce a `TenantId` concept, tenant-scoped filtering, or per-tenant configuration sections. If a future requirement needs multi-tenancy, that's a deliberate architectural decision to make explicitly — not something to bolt on incrementally.

## 5. Messaging: Always Through IMessagePublisher / IMessageConsumer

Feature code must never talk to RabbitMQ (or any transport) directly:

```csharp
public record BrokerMessage(string? Id, string Sender, string Receiver, string Content, string? PartitionKey = null, string MessageType = "");

public interface IMessagePublisher
{
    Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default);
}

public interface IMessageConsumer
{
    Task RunAsync(Func<BrokerMessage, CancellationToken, Task> handler, CancellationToken cancellationToken);

    Task StartPartitionAsync(
        string partitionKey,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);

    Task StopPartitionAsync(string partitionKey, CancellationToken cancellationToken = default);
}
```

`TsiBroker.Ru.Api` and `TsiBroker.Im.Api` register their `IMessagePublisher` via `services.AddMessagePublisher(configuration)`; `TsiBroker.ApiService` additionally registers an `IMessageConsumer` via `services.AddMessageConsumer(configuration)` (both extension methods in `TsiBroker.Core/Messaging/MessagePublisherServiceCollectionExtensions.cs`) instead of an inline `AddSingleton<...>()`. Both pick their concrete implementation based on `Messaging:QueueType` (env var `Messaging__QueueType`, `MessageQueueType` enum: `RabbitMq` (default) or `Debug`), so switching backends is a config change, not a code change:

- `RabbitMq` → `RabbitMqMessagePublisher` / `RabbitMqMessageConsumer` (`TsiBroker.Core/Messaging/`). Both lazily open one shared connection per process, and declare queue topology via the shared `RabbitMqTopology.DeclareAsync` helper so identical queue arguments are used everywhere a given queue is declared (RabbitMQ rejects a re-declare with different arguments).
- `Debug` → `DebugMessagePublisher` / `DebugMessageConsumer` (log-only, no-op) — for local debugging without a broker.

**Per-partition queues (ordering, but not globally serialized):** `BrokerMessage.PartitionKey` groups messages that must stay in order relative to each other — e.g. `TsiMessageEndpoints.cs` (RU→broker flow) sets it to the recipient `InfrastructureOperator.Name` (resolved via the RICS code the message used as `Recipient`), so every message addressed to one Infrastrukturbetreiber lands in the *same* queue, while different operators get independent queues.

- `RabbitMqMessagePublisher.PublishAsync` routes a message with a `PartitionKey` to a queue named by `RabbitMqQueueNaming.ForPartition(partitionKey)` — a slugified (umlaut/diacritic-safe, ASCII, lowercase) version of the key plus `-out`, e.g. `"ÖBB Infrastruktur AG"` → `obb-infrastruktur-ag-out`. Queues are declared on demand, the first time each partition key is seen, and memoized so later publishes skip the redundant declare. A message without a `PartitionKey` falls back to the single default queue, `RabbitMqOptions.QueueName`. The IM→broker/CI flow uses `"evu:{RailwayUndertaking.Name}"` as its partition key — prefixed so this direction's queues never collide with the plain-`Name` partition keys the RU→broker direction uses for Infrastrukturbetreiber.
- `RabbitMqMessageConsumer.StartPartitionAsync(partitionKey, handler)` opens one dedicated **channel** for that partition key (on the shared connection) and runs an independent `prefetchCount: 1` consume loop on it — so a message stuck retrying in one operator's queue never delays another operator's queue; only *within* one operator's queue is processing still strictly serialized. The loop keeps running in the background until `StopPartitionAsync(partitionKey)` is called or the consumer is disposed — it does **not** stop when the `cancellationToken` passed to `StartPartitionAsync` is cancelled, since that token only bounds the (synchronous) setup, not the partition's lifetime. Both methods are idempotent (starting an already-running partition, or stopping one that isn't running, is a no-op). `RunAsync` (no partitioning) is the degenerate case: one loop, on `RabbitMqOptions.QueueName`, tied to the passed-in `cancellationToken` for its whole lifetime.
- Two Infrastrukturbetreiber names that slugify to the same value would end up sharing a queue (`Name` isn't enforced unique in `InfrastructureOperatorStore`) — not handled specially; acceptable at current operator counts, worth revisiting (e.g. enforce `Name` uniqueness in the admin API) if/when that becomes a real collision risk.

**Live start/stop tied to Infrastrukturbetreiber lifecycle:** `TsiBroker.ApiService`'s `InfrastructureOperatorConsumerCoordinator` (`TsiBroker.ApiService/InfrastructureOperators/InfrastructureOperatorConsumerCoordinator.cs`) keeps each operator's partition consumption in sync with `InfrastructureOperatorStore`, called from `InfrastructureOperatorEndpoints.cs`:
- **Create** → `StartAsync` (new operators are always created active)
- **`PATCH /{id}/status`** → `StartAsync` if `IsActive: true`, `StopAsync` if `false`
- **`PUT /{id}` (rename)** → if `Name` changed, `StopAsync(previousName)` + `StartAsync(updated)` — renaming an active operator changes its queue name, so this migrates live consumption instead of leaving the old queue's consumer running under a name nothing publishes to anymore
- **Delete** → `StopAsync`
- **Process startup** (`Program.cs`, right after `app.Build()`) → `StartAllActiveAsync` over every currently-active operator, so a restart doesn't silently stop delivery until the next CRUD call

`InfrastructureOperatorConsumerCoordinator.HandleMessageAsync` (the handler passed to `StartPartitionAsync`) is currently just a logging placeholder — `"Would relay message ... (relay-to-IM not implemented yet)"` — the same role `DebugMessagePublisher` plays on the publish side, standing in until the real RU→IM/IM→RU relay logic exists (see gap #1 below).

**Retry / dead-letter policy** (consumer only, per queue): on a handler exception, the message is retried in place up to `RabbitMqOptions.MaxDeliveryAttempts` (default 3) with a `RetryDelay` (default 2s) between attempts — no other message *on that same queue* is processed while this is happening (other partitions' queues are unaffected). After the last attempt still fails, the message is `Nack`'d without requeue, which RabbitMQ routes (via `x-dead-letter-exchange`/`x-dead-letter-routing-key`, set per queue by `RabbitMqTopology.DeclareAsync`) straight to that queue's dead-letter queue (`RabbitMqTopology.DeadLetterQueueNameFor(queueName)`, i.e. `"{queueName}.dead-letter"`) for manual inspection, and the consumer moves on to the next message on that queue.

A handler can also publish directly into its own partition's dead-letter queue via `IMessagePublisher.PublishToDeadLetterAsync(partitionKey, message)` — used when a handler already knows a message can't be delivered (e.g. failed authorization) and wants to route it there without going through the retry-then-nack cycle. This is how `EvuDeliveryCoordinator` implements "move to the customer's error queue": there's no separate error-queue concept, it's the same dead-letter queue the generic retry mechanism already uses.

**Pausing a partition without losing a message:** a handler that wants to stop its own partition — because it's discovered the downstream system is unreachable, not just that one message failed — throws `MessagePausedException` (`TsiBroker.Core/Messaging/MessagePausedException.cs`) instead of a normal exception. `RabbitMqMessageConsumer.ProcessDeliveryAsync` special-cases it: the message is `Nack`'d **with** `requeue: true` (unlike the dead-letter path above) and processing of that delivery stops immediately, without counting against `MaxDeliveryAttempts`. The handler itself is responsible for actually stopping the partition (`IMessageConsumer.StopPartitionAsync`) — done asynchronously via `Task.Run`, *not* awaited inline in the handler, because closing the very channel `ProcessDeliveryAsync` is about to `Nack` on would race the ack/nack call. `EvuDeliveryCoordinator` uses this when an EVU's `/health` check fails, so the message that triggered the pause is the first one redelivered once processing resumes — see [[Business-Flow]] Flow 4.

Adding a new backend (e.g. AWS Service Bus/SQS) means adding an enum case to `MessageQueueType`, `IMessagePublisher`/`IMessageConsumer` implementations + their own `Options` type in `Messaging/`, and a `case` in `AddMessagePublisher`/`AddMessageConsumer` — feature code calling `IMessagePublisher.PublishAsync` or providing an `IMessageConsumer` handler never changes. See [[Business-Flow]] for exactly which flows currently call this and what's still missing end-to-end.

RabbitMQ connection settings live in `RabbitMqOptions` (`SectionName = "RabbitMq"`): `HostName`, `Port`, `UseTls`, `VirtualHost`, `UserName`, `Password`, `QueueName` (default/non-partitioned queue only), `MaxDeliveryAttempts`, `RetryDelay`. Following the options pattern in §8, these — plus `Messaging:QueueType` — are meant to be set per environment via env vars (`Messaging__QueueType`, `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password`, etc.) rather than committed to `appsettings.json`.

> **Breaking change notes:**
> 1. Queue arguments now include `x-dead-letter-exchange`/`x-dead-letter-routing-key` (see `RabbitMqTopology`). RabbitMQ queue arguments are immutable after declaration — if a queue already exists from a pre-dead-letter-queue deployment, the app will fail to redeclare it (`PRECONDITION_FAILED`) until that queue is deleted (it'll be recreated automatically with the new arguments on next connect).
> 2. Since `TsiMessageEndpoints.cs` now always sets `PartitionKey`, RU→broker traffic no longer flows through the shared `tsi-messages` queue at all — it goes straight to the recipient Infrastrukturbetreiber's own `{slug}-out` queue. `tsi-messages` is still declared/used for messages without a `PartitionKey` (currently only the IM→broker/CI flow).

## 6. SOAP Contract Code Stays Generated-Code-Adjacent

`TsiBroker.Im.Api/CI` and `.../Heartbeat` each split into:

- `Generated/` — WSDL-derived types and the raw `.wsdl` file itself. **Do not hand-edit these.** For Heartbeat, the generated type (`Heartbeat.cs`) lives here in `TsiBroker.Im.Api/Heartbeat/Generated/`. For CI, the generated type (`CommonInterface.cs`) instead lives in `TsiBroker.Im.Core/CI/Generated/` — pulled out into its own project because `TsiBroker.Im.Mock` (the ISB test double, see [[Architecture]]) needs the same CI types and shouldn't depend on `TsiBroker.Im.Api`; `TsiBroker.Im.Api/CI/Generated/` today only holds the raw `.wsdl`.
- `Contracts/` — hand-written `[MessageContract]` wrapper types (`CommonInterfaceRequest`, `CommonInterfaceResponse`, etc.) that map the generated types onto CoreWCF message shapes
- `I...MessageService.cs` + `...MessageService.cs` — the hand-written service contract and its implementation, as sibling files at the feature root (not inside `Generated/`)

If the upstream WSDL changes and types need regenerating, only the `Generated/` folders (in `TsiBroker.Im.Api` and, for CI, `TsiBroker.Im.Core`) should be touched by the regeneration step; `Contracts/` and the service implementation are yours to maintain by hand.

## 7. Validation and Error Responses

- Admin API (`TsiBroker.ApiService`): simple manual validation (non-blank checks, trimming, `Distinct()` normalization) directly in the endpoint handler, returning `Results.BadRequest(...)` — no FluentValidation or similar library is used
- RU API (`TsiBroker.Ru.Api`): failures are returned as RFC 7807 `Results.Problem(...)` with a `title`/`detail`/`statusCode` tailored to the specific failure reason (see `TsiMessageAuthorizationFailureReason` → `ToProblemResult` in `TsiMessageEndpoints.cs`) — prefer this pattern (an enum of failure reasons mapped to problem responses) over exceptions for expected, user-facing failure modes
- IM API (`TsiBroker.Im.Api`): SOAP faults aren't used for expected failures; instead a synthesized ACK/NACK response is returned even on error, per the Common Interface spec's `LI_TechnicalAck` shape

## 8. Options Pattern for Configuration

Every configurable component (stores, admin user) follows the same shape: an `XyzOptions` class with a `public const string SectionName = "..."`, bound via `AddOptions<XyzOptions>().Bind(configuration.GetSection(XyzOptions.SectionName))`, injected as `IOptions<XyzOptions>`. Keep new configuration following this pattern rather than reading `IConfiguration` ad-hoc inside feature code.
