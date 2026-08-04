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

TsiBroker.AppHost
├── TsiBroker.ApiService
├── TsiBroker.Im.Api
├── TsiBroker.Ru.Api
└── TsiBroker.Ui (wired via AddViteApp, not a project reference)
```

`TsiBroker.Core` is referenced by all three backend hosts and holds everything they share: the two domain entities, their JSON-file stores, and the `IMessagePublisher` messaging abstraction. `TsiBroker.Im.Api` does **not** use the stores at all today (see [[Business-Flow]]).

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

### TsiBroker.AppHost

.NET Aspire orchestration for local dev (`src/TsiBroker.AppHost/AppHost.cs`). Wires up four resources: `apiservice`, `InfrastructureManagement-api`, `RailwayUndertaking-api`, and `ui` (via `AddViteApp`, not a project reference). A notable detail: `apiservice` and `RailwayUndertaking-api` are pointed at the **same** `App_Data` directory (under `TsiBroker.ApiService/App_Data`) via matching `RailwayUndertakings__DataDirectory` / `InfrastructureOperators__DataDirectory` environment variables, since they read/write the same flat-file store. `TsiBroker.Im.Api` doesn't get these variables — it doesn't touch the stores. There is **no RabbitMQ resource** registered here; local Aspire runs always use `DebugMessagePublisher`.

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
