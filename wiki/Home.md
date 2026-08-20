# TsiBroker Wiki

**Message broker between railway undertakings (RU/EVU) and infrastructure managers (IM/ISB)**

TsiBroker relays TAF/TAP-TSI messages — Common Interface (CI) and Heartbeat — between Railway Undertakings and Infrastructure Managers. RUs talk to the broker over REST with an API key; IMs talk to the broker over SOAP (CoreWCF). An admin UI manages the master data (which RUs and IMs exist, and which message types each RU is authorized to exchange with each IM).

---

## Quick Links

| Document | Description |
|----------|-------------|
| [[Architecture]] | Solution layout, project responsibilities, feature-folder convention, messaging abstraction |
| [[Data-Model]] | Entities (`InfrastructureOperator`, `RailwayUndertaking`, `IsbAssignment`, `PartnerCertificateBundle`), JSON-file storage |
| [[Business-Flow]] | End-to-end message flow for CI, Heartbeat, and RU→broker submissions; authorization rules |
| [[External-API-Guide]] | Integration guide for RU (REST) and IM (SOAP) systems connecting to the broker |
| [[Frontend-Architecture]] | Vue 3 admin UI structure |
| [[Backend-Best-Practices]] | Backend coding conventions |

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Backend | .NET 10, ASP.NET Core Minimal APIs |
| Orchestration | .NET Aspire (local dev) |
| Persistence | JSON files under `App_Data/` — no database |
| IM-side transport | SOAP/WCF via CoreWCF (Common Interface, Heartbeat, generated from WSDL) |
| RU-side transport | REST + `X-Api-Key` header |
| Admin auth | Single admin user, cookie-based, no external IdP |
| Messaging | RabbitMQ, wired up as the default publisher/consumer backend — ingestion and per-operator queueing work; the consumer-side relay onward is still a logging placeholder — see [[Business-Flow]] |
| Frontend | Vue 3, Pinia, vue-router, vue-i18n, Less (no Vuetify, no TanStack Query, no generated API client) |
| Containers | podman (not docker) |

## Solution Structure

```
TsiBroker.slnx
└── src/
    ├── TsiBroker.Core/          # Domain entities, JSON-file stores, messaging abstractions
    ├── TsiBroker.ApiService/    # Admin REST API + cookie auth (consumed by TsiBroker.Ui)
    ├── TsiBroker.Ru.Api/        # REST API exposed to Railway Undertakings (X-Api-Key)
    ├── TsiBroker.Im.Api/        # SOAP endpoints exposed to Infrastructure Managers (CoreWCF)
    ├── TsiBroker.Im.Core/       # Shared CI contract types (TsiBroker.Im.Api + TsiBroker.Im.Mock)
    ├── TsiBroker.Im.Mock/       # Test double for a real ISB, + TsiBroker.Im.Mock.UI frontend
    ├── TsiBroker.Ru.Mock/       # Test double for a real EVU, + TsiBroker.Ru.Mock.UI frontend
    ├── TsiBroker.Ui/            # Vue 3 admin frontend
    └── TsiBroker.AppHost/       # Aspire orchestration for local dev
```

See [[Architecture]] for what each project actually does and how they depend on each other.

## Current State / Known Gaps

This is an early-stage project. Worth knowing before you dig into the code:

- **No end-to-end message relay yet.** `TsiBroker.Ru.Api` and `TsiBroker.Im.Api` publish incoming messages to RabbitMQ (`RabbitMqMessagePublisher`, the default `IMessagePublisher`), and `TsiBroker.ApiService` consumes per-operator queues, but the consumer's handler is still a logging placeholder — no code yet calls out to an RU's or IM's `SystemUrl`. See [[Business-Flow]] for the full picture.
- **Heartbeat messages are never published**, only logged and echoed back — unlike Common Interface messages, which do build a `BrokerMessage`.
- **No database** — `InfrastructureOperator` and `RailwayUndertaking` records live in two JSON files under `App_Data/`, guarded by an in-process lock. This is fine for a single-instance deployment but doesn't scale horizontally.
- **One `.NET` test project exists: `TsiBroker.Im.Api.Tests`** (xUnit, integration-style — spins up a real Kestrel host to cover client-certificate enforcement). No other project has test coverage yet.
- **Certificate storage/validation exists and is partially wired in.** `PartnerCertificateBundle`/`CertificateBundleStore`/`PartnerCertificateProvider`/`PartnerCertificateValidator` (see [[Data-Model]]#PartnerCertificateBundle) let an admin upload and manage per-IM client/CA certificates, and `CertificateExpiryMonitor` logs expiry warnings. Inbound: `TsiBroker.Im.Api`'s `/ci` endpoint now requests a client certificate and rejects 2-way partners that don't present one (TICKET-1, presence-only — see [[Architecture]]); `/heartbeat` is not covered (no sender identity in its wire format), and the actual CA/CN/CRL trust check is still TICKET-2. Outbound: `IsbApiClient` still sends mTLS calls as plain HTTPS — that's TICKET-3/4.
- Only `TsiBroker.ApiService` (the admin backend) is containerized in `infrastructure/docker-compose.yml`; `TsiBroker.Ru.Api` and `TsiBroker.Im.Api` have no Docker deployment path yet.

## Development

See [AGENTS.md](../AGENTS.md) at the repo root for day-to-day conventions (language rules, wiki maintenance policy, quick reference commands). Short version:

```bash
# Start everything with Aspire (recommended)
dotnet run --project src/TsiBroker.AppHost

# Frontend standalone
cd src/TsiBroker.Ui && npm install && npm run dev

# Containers (api, ui, rabbitmq)
cd infrastructure && podman compose --env-file .env.example up -d --build
```

### Test Users (local dev / docker-compose)

| Username | Password | Source |
|---|---|---|
| `sa` | `temp` | `TsiBroker.ApiService/appsettings.json` (`AdminUser` section) |
| `sa` | `temp!` (default) | `infrastructure/.env.example` (overridable per env file) |
