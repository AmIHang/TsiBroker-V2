# Architecture

## Overview

TsiBroker is a message broker sitting between **Railway Undertakings (RU/EVU)** and **Infrastructure Managers (IM/ISB)**, relaying TAF/TAP-TSI messages (Common Interface, Heartbeat). It is deliberately small: no database, no multi-tenancy, minimal APIs only, and each backend project is a thin, single-purpose service rather than a layered monolith.

**Architecture style:** Feature folders per project — each feature (`Auth/`, `InfrastructureOperators/`, `RailwayUndertakings/`, `Messages/`, `WhoAmI/`, `CI/`, `Heartbeat/`) owns its entity/store, request/response records, and endpoint mapping together, instead of splitting across `Controllers/`, `Services/`, `DTOs/` layers.

---

## Solution Structure

```
TsiBroker.slnx
└── src/
    ├── TsiBroker.Core/          # Domain entities, JSON-file stores, messaging abstractions
    ├── TsiBroker.ApiService/    # Admin REST API + cookie auth
    ├── TsiBroker.Ru.Api/        # REST API for Railway Undertakings (X-Api-Key auth)
    ├── TsiBroker.Im.Api/        # SOAP API for Infrastructure Managers (CoreWCF)
    ├── TsiBroker.Im.Core/       # Shared CI (Common Interface) contract types, used by TsiBroker.Im.Api and TsiBroker.Im.Mock
    ├── TsiBroker.Im.Mock/       # Test double for a real ISB (plays both directions of the CI contract)
    ├── TsiBroker.Im.Mock.UI/    # Vue 3 UI for TsiBroker.Im.Mock
    ├── TsiBroker.Ru.Mock/       # Test double for a real EVU (plays both directions of the RU REST contract)
    ├── TsiBroker.Ru.Mock.UI/    # Vue 3 UI for TsiBroker.Ru.Mock
    ├── TsiBroker.Ui/            # Vue 3 admin frontend
    └── TsiBroker.AppHost/       # Aspire orchestration for local dev
```

### Project Dependencies

```
TsiBroker.Core
└── no project dependencies (Microsoft.Extensions.Hosting/Logging/Options only)

TsiBroker.ApiService ──┐
TsiBroker.Ru.Api ──────┼── TsiBroker.Core
TsiBroker.Im.Api ──────┘

TsiBroker.Im.Core
└── no project dependencies

TsiBroker.Im.Api ──┐
TsiBroker.Im.Mock ──┴── TsiBroker.Im.Core

TsiBroker.Ru.Mock
└── no project dependencies (self-contained; doesn't reference TsiBroker.Core)

TsiBroker.AppHost
├── TsiBroker.ApiService
├── TsiBroker.Im.Api
├── TsiBroker.Im.Mock
├── TsiBroker.Ru.Api
├── TsiBroker.Ru.Mock
├── TsiBroker.Ui (wired via AddViteApp, not a project reference)
├── TsiBroker.Im.Mock.UI (wired via AddViteApp, not a project reference)
└── TsiBroker.Ru.Mock.UI (wired via AddViteApp, not a project reference)
```

`TsiBroker.Core` is referenced by the three main backend hosts (`ApiService`, `Ru.Api`, `Im.Api`) and holds everything they share: the two domain entities, their JSON-file stores, and the `IMessagePublisher`/`IMessageConsumer` messaging abstractions. `TsiBroker.Im.Api` does **not** use the stores at all today (see [[Business-Flow]]). The mock projects sit outside this — `TsiBroker.Im.Mock` shares only `TsiBroker.Im.Core` (the CI contract types) with `TsiBroker.Im.Api`, and `TsiBroker.Ru.Mock` has no project dependencies at all.

---

## Project Responsibilities

### TsiBroker.Core

Shared kernel. No web framework references — just domain + persistence + messaging abstractions.

- `InfrastructureOperators/` — `InfrastructureOperator` entity + `InfrastructureOperatorStore` (JSON file `infrastructure-operators.json`)
- `RailwayUndertakings/` — `RailwayUndertaking` + `IsbAssignment` entities + `RailwayUndertakingStore` (JSON file `railway-undertakings.json`)
- `Messaging/` — `BrokerMessage` record, `IMessagePublisher` interface, `DebugMessagePublisher` (the only current implementation)

See [[Data-Model]] for entity shapes and the store implementation.

### TsiBroker.ApiService (admin backend)

Cookie-authenticated REST API consumed exclusively by `TsiBroker.Ui`. Owns CRUD for both entities:

- `Auth/` — single hard-coded admin account (`AdminUserOptions`, config section `AdminUser`), cookie scheme `TsiBroker.Auth` (`HttpOnly`, `SameSite=None`, `Secure=Always`, 10-minute sliding expiration), timing-safe password comparison via `CryptographicOperations.FixedTimeEquals`
- `InfrastructureOperators/` — `GET/POST /api/infrastructure-operators`, `PUT/PATCH/DELETE /{id}`
- `RailwayUndertakings/` — same CRUD shape plus `GET /generate-api-key` (stateless key generation) and cascading cleanup of `IsbAssignment`s when an operator is deleted

All endpoints require authentication (`.RequireAuthorization()` on the route group) except login itself.

### TsiBroker.Ru.Api (RU-facing REST API)

Exposes two endpoints to Railway Undertakings, authenticated via a manually-checked `X-Api-Key` header (there is no ASP.NET Core authentication scheme registered for this — the header is read and validated inside each endpoint handler, not via middleware):

- `Messages/` — `POST /message`: accepts raw TAF/TAP XML, parses the `MessageHeader`, authorizes it against the caller's `RailwayUndertaking` and its `IsbAssignment`s (`TsiMessageAuthorizationService`), and hands the result to `IMessagePublisher`
- `WhoAmI/` — `GET /whoami`: self-service discovery endpoint returning (as XML) which infrastructure operators and message types the calling RU is authorized for

### TsiBroker.Im.Api (IM-facing SOAP API)

CoreWCF-hosted SOAP service with two endpoints, both `BasicHttpBinding` over HTTPS transport security, **no authentication configured**:

- `/ci` — Common Interface (`CI/`), wraps incoming `UICMessage`s into a `BrokerMessage` and returns a synthesized ACK/NACK
- `/heartbeat` — Heartbeat (`Heartbeat/`), pure liveness echo — logged only, never published

Contract code under `CI/Generated/` and `Heartbeat/Generated/` is WSDL-derived and treated as generated code; hand-written service logic lives in sibling `*MessageService.cs` files, not inside `Generated/`.

### TsiBroker.Ui

Vue 3 SPA. See [[Frontend-Architecture]].

### TsiBroker.Im.Core

Shared kernel for the CI (Common Interface) contract, referenced by both `TsiBroker.Im.Api` and `TsiBroker.Im.Mock` so they agree on the same WSDL-derived types without one depending on the other. Holds `CI/Generated/CommonInterface.cs` (do not hand-edit — see [[Backend-Best-Practices]] §6) and `CI/TechnicalAckFactory.cs`. Note this only covers CI — the Heartbeat contract's generated code still lives locally inside `TsiBroker.Im.Api/Heartbeat/Generated/`, since only `Im.Api` needs it.

### TsiBroker.Im.Mock / TsiBroker.Im.Mock.UI

A test double for a real Infrastructure Manager system — plays both directions of the Common Interface contract (sending to `TsiBroker.Im.Api`'s `/ci` today; receiving on its own `/ci` once the outbound relay from the broker exists). Lets you develop/test against an ISB without standing up a real one. `TsiBroker.Im.Mock.UI` is its Vue 3 frontend, sharing chrome/styles with `TsiBroker.Ui` via `@tsibroker/ui-kit` — see [[Frontend-Architecture]].

### TsiBroker.Ru.Mock / TsiBroker.Ru.Mock.UI

A test double for a real Railway Undertaking system — plays both directions of the RU REST contract (sending to `TsiBroker.Ru.Api`'s `/message` today; already implementing the receiving side, `POST /message` and `GET /config/update`, for when the broker→RU relay exists — see [[External-API-Guide]]). `TsiBroker.Ru.Mock.UI` is its Vue 3 frontend (Response Settings, Received Messages log), sharing chrome/styles with `TsiBroker.Ui` via `@tsibroker/ui-kit` — see [[Frontend-Architecture]].

### TsiBroker.AppHost

.NET Aspire orchestration for local dev (`src/TsiBroker.AppHost/AppHost.cs`). Wires up eight resources: `apiservice`, `InfrastructureManagement-api`, `isb-mock` (`TsiBroker.Im.Mock`), `RailwayUndertaking-api`, `evu-mock` (`TsiBroker.Ru.Mock`), and three Vite apps — `ui`, `isb-mock-ui`, `evu-mock-ui` (all via `AddViteApp`, not project references). A notable detail: `apiservice` and `RailwayUndertaking-api` are pointed at the **same** `App_Data` directory (under `TsiBroker.ApiService/App_Data`) via matching `RailwayUndertakings__DataDirectory` / `InfrastructureOperators__DataDirectory` environment variables, since they read/write the same flat-file store. `TsiBroker.Im.Api` doesn't get these variables — it doesn't touch the stores. There is **no RabbitMQ resource** registered in AppHost — but note that `MessagingOptions.QueueType` now defaults to `RabbitMq` (not `Debug`) and no project overrides it back to `Debug` for local dev, so an Aspire run needs a reachable RabbitMQ instance (e.g. via `infrastructure/docker-compose.yml`) unless `Messaging__QueueType=Debug` is set explicitly. See [[Backend-Best-Practices]] §5 for the messaging backend and [[Business-Flow]] for what's actually wired up end-to-end.

---

## Persistence: JSON-File Stores, Not a Database

There is intentionally no database. `InfrastructureOperatorStore` and `RailwayUndertakingStore` (both in `TsiBroker.Core`) are plain singleton classes that read/write a JSON file on every operation:

- Default location: `<ContentRootPath>/App_Data/{infrastructure-operators,railway-undertakings}.json` — overridable per-store via `IOptions<T>` (`InfrastructureOperators:DataDirectory`, `RailwayUndertakings:DataDirectory`)
- Concurrency: a single `SemaphoreSlim(1, 1)` per store guards **every** read-modify-write, including plain reads — simple mutual exclusion, not reader/writer locking
- Writes are full-file overwrites (`JsonSerializer.SerializeAsync` with `WriteIndented = true`) — no atomic rename, no backup
- Referential integrity between the two entities (an `IsbAssignment` referencing an `InfrastructureOperator`) is enforced at the endpoint layer, not by the storage layer — e.g. deleting an `InfrastructureOperator` triggers `RailwayUndertakingStore.RemoveInfrastructureOperatorAssignmentsAsync` to strip dangling assignments

In containers, `App_Data` is mounted to a named volume (`api_data`) so data survives container recreation.

**Implication:** this only works correctly with a single instance of `TsiBroker.ApiService`/`TsiBroker.Ru.Api` per `App_Data` directory. There's no distributed locking — don't scale these out horizontally without replacing this layer first.

---

## Messaging Abstraction

`TsiBroker.Core/Messaging/IMessagePublisher` is the single abstraction feature code depends on:

```csharp
public interface IMessagePublisher
{
    Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default);
}

public record BrokerMessage(string? Id, string Sender, string Receiver, string Content);
```

Feature code must never call RabbitMQ (or any transport) directly — always go through `IMessagePublisher`. Today the only registered implementation everywhere is `DebugMessagePublisher`, which logs `"Would publish message {MessageId} from {Sender}: {Payload}"` and returns `Task.CompletedTask`. See [[Business-Flow]] for exactly which code paths call this and what's still missing before messages actually reach the other side.

---

## Non-Goals (by design, per `AGENTS.md`)

- No Repository/DbContext pattern
- No multi-tenancy — this is a single-tenant broker
- No MVC controllers — Minimal APIs only
- No generated frontend API client — all calls go through `src/TsiBroker.Ui/src/lib/api.ts`
