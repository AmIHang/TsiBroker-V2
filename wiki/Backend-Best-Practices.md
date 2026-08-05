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
public record BrokerMessage(string? Id, string Sender, string Receiver, string Content);

public interface IMessagePublisher
{
    Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default);
}

public interface IMessageConsumer
{
    Task RunAsync(Func<BrokerMessage, CancellationToken, Task> handler, CancellationToken cancellationToken);
}
```

`TsiBroker.Ru.Api` and `TsiBroker.Im.Api` register their `IMessagePublisher` via `services.AddMessagePublisher(configuration)`; a process that needs to read messages instead (none does yet — see below) calls `services.AddMessageConsumer(configuration)` (both in `TsiBroker.Core/Messaging/MessagePublisherServiceCollectionExtensions.cs`) instead of an inline `AddSingleton<...>()`. Both pick their concrete implementation based on `Messaging:QueueType` (env var `Messaging__QueueType`, `MessageQueueType` enum: `RabbitMq` (default) or `Debug`), so switching backends is a config change, not a code change:

- `RabbitMq` → `RabbitMqMessagePublisher` / `RabbitMqMessageConsumer` (`TsiBroker.Core/Messaging/`). The publisher publishes `BrokerMessage.Content` (raw XML) as a persistent message to a durable queue on RabbitMQ's default exchange. The consumer reads with `prefetchCount: 1` and manual ack — it never lets the broker push the next message until the current one is ack'd or dead-lettered, which is what keeps processing (including retries) in strict queue order, e.g. two messages from the same RU never overtake each other. Both lazily open one shared connection/channel per process, and both declare the queue topology via the shared `RabbitMqTopology.DeclareAsync` helper so their (identical) queue arguments never conflict with each other's declaration.
- `Debug` → `DebugMessagePublisher` / `DebugMessageConsumer` (log-only, no-op) — for local debugging without a broker.

**Retry / dead-letter policy** (consumer only): on a handler exception, `RabbitMqMessageConsumer` retries the same message in place up to `RabbitMqOptions.MaxDeliveryAttempts` (default 3) with a `RetryDelay` (default 2s) between attempts — no other message is processed while this is happening. After the last attempt still fails, the message is `Nack`'d without requeue, which RabbitMQ routes (via `x-dead-letter-exchange`/`x-dead-letter-routing-key` set on the main queue) straight to `RabbitMqOptions.DeadLetterQueueName` (default `tsi-messages.dead-letter`) for manual inspection, and the consumer moves on to the next message.

> **Ordering tradeoff:** this is a single queue with a single consumer (`prefetchCount: 1`), so ordering is enforced globally, not just per-sender — a message stuck retrying for one RU also delays unrelated messages for other ROs behind it in the same queue. That's a deliberate simplification for the initial implementation; per-sender parallelism (e.g. one queue/consumer per RU, or a partitioned/sharded consumer) would need a bigger redesign and hasn't been requested.

Adding a new backend (e.g. AWS Service Bus/SQS) means adding an enum case to `MessageQueueType`, `IMessagePublisher`/`IMessageConsumer` implementations + their own `Options` type in `Messaging/`, and a `case` in `AddMessagePublisher`/`AddMessageConsumer` — feature code calling `IMessagePublisher.PublishAsync` or providing an `IMessageConsumer` handler never changes. See [[Business-Flow]] for exactly which flows currently call this and what's still missing end-to-end — in particular, **no host currently calls `AddMessageConsumer`/`IMessageConsumer.RunAsync`**; the consumer side exists in `TsiBroker.Core` but isn't wired into a running process yet, since the actual relay-to-RU/IM handler logic doesn't exist yet either.

RabbitMQ connection settings live in `RabbitMqOptions` (`SectionName = "RabbitMq"`): `HostName`, `Port`, `UseTls`, `VirtualHost`, `UserName`, `Password`, `QueueName`, `MaxDeliveryAttempts`, `RetryDelay`, `DeadLetterQueueName`. Following the options pattern in §8, these — plus `Messaging:QueueType` — are meant to be set per environment via env vars (`Messaging__QueueType`, `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password`, etc.) rather than committed to `appsettings.json`.

> **Breaking change note:** the main queue's arguments now include `x-dead-letter-exchange`/`x-dead-letter-routing-key` (see `RabbitMqTopology`). RabbitMQ queue arguments are immutable after declaration — if `tsi-messages` already exists from a pre-dead-letter-queue deployment, the app will fail to redeclare it (`PRECONDITION_FAILED`) until that queue is deleted (it'll be recreated automatically with the new arguments on next connect).

## 6. SOAP Contract Code Stays Generated-Code-Adjacent

`TsiBroker.Im.Api/CI` and `.../Heartbeat` each split into:

- `Generated/` — WSDL-derived types (`CommonInterface.cs`, `Heartbeat.cs`) and the raw `.wsdl` file itself. **Do not hand-edit these.**
- `Contracts/` — hand-written `[MessageContract]` wrapper types (`CommonInterfaceRequest`, `CommonInterfaceResponse`, etc.) that map the generated types onto CoreWCF message shapes
- `I...MessageService.cs` + `...MessageService.cs` — the hand-written service contract and its implementation, as sibling files at the feature root (not inside `Generated/`)

If the upstream WSDL changes and types need regenerating, only `Generated/` should be touched by the regeneration step; `Contracts/` and the service implementation are yours to maintain by hand.

## 7. Validation and Error Responses

- Admin API (`TsiBroker.ApiService`): simple manual validation (non-blank checks, trimming, `Distinct()` normalization) directly in the endpoint handler, returning `Results.BadRequest(...)` — no FluentValidation or similar library is used
- RU API (`TsiBroker.Ru.Api`): failures are returned as RFC 7807 `Results.Problem(...)` with a `title`/`detail`/`statusCode` tailored to the specific failure reason (see `TsiMessageAuthorizationFailureReason` → `ToProblemResult` in `TsiMessageEndpoints.cs`) — prefer this pattern (an enum of failure reasons mapped to problem responses) over exceptions for expected, user-facing failure modes
- IM API (`TsiBroker.Im.Api`): SOAP faults aren't used for expected failures; instead a synthesized ACK/NACK response is returned even on error, per the Common Interface spec's `LI_TechnicalAck` shape

## 8. Options Pattern for Configuration

Every configurable component (stores, admin user) follows the same shape: an `XyzOptions` class with a `public const string SectionName = "..."`, bound via `AddOptions<XyzOptions>().Bind(configuration.GetSection(XyzOptions.SectionName))`, injected as `IOptions<XyzOptions>`. Keep new configuration following this pattern rather than reading `IConfiguration` ad-hoc inside feature code.
