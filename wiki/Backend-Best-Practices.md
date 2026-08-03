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

## 5. Messaging: Always Through IMessagePublisher

Feature code must never talk to RabbitMQ (or any transport) directly:

```csharp
public record BrokerMessage(string? Id, string Sender, string Receiver, string Content);

public interface IMessagePublisher
{
    Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default);
}
```

`DebugMessagePublisher` (log-only, no-op) is currently the sole registered implementation in every host. When a real transport-backed implementation is added, it should be a drop-in replacement registered in `Program.cs` — no feature code should need to change. See [[Business-Flow]] for exactly which flows currently call this and what's still missing end-to-end.

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
