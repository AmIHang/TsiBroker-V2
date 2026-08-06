# AGENTS.md

Guidance for any AI coding agent (Claude Code, GitHub Copilot, Cursor, etc.) working in this repository. This is the single source of truth for project conventions — read it before making changes.

## Project Overview

**TsiBroker** is a message broker between railway undertakings (RU/EVU) and infrastructure managers (IM/ISB), relaying TAF/TAP-TSI messages (e.g. Common Interface, Heartbeat) between the two sides.

**Tech Stack**
- Backend: .NET 10, ASP.NET Core Minimal APIs, Aspire (orchestration)
- Persistence: no database — JSON files under `App_Data/` (see `InfrastructureOperatorStore`, `RailwayUndertakingStore` in `TsiBroker.Core`), guarded by an in-process `SemaphoreSlim`. In containers, `App_Data` is mounted to a volume so data survives recreation.
- Messaging: RabbitMQ (`infrastructure/docker-compose.yml`) behind `IMessagePublisher`/`IMessageConsumer`, one queue per Infrastrukturbetreiber (`TsiBroker.ApiService`'s `InfrastructureOperatorConsumerCoordinator` starts/stops consumption as operators are created/activated/deactivated/renamed/deleted); `DebugMessagePublisher`/`DebugMessageConsumer` are no-op/logging alternatives, selectable via `Messaging:QueueType`
- IM-side transport: SOAP/WCF via `CoreWCF` (`TsiBroker.Im.Api` — Common Interface, Heartbeat, generated from WSDL)
- RU-side transport: REST + `X-Api-Key` header auth (`TsiBroker.Ru.Api`)
- Admin UI backend auth: single admin user (`AdminUser` config section), cookie-based (`TsiBroker.Auth` cookie), no external IdP
- Frontend: Vue 3, Pinia, vue-router, vue-i18n, Less (no Vuetify, no TanStack Query, no generated API client)
- Infrastructure: containers via `podman` (not `docker`) — see `infrastructure/compose.md`

## Solution Layout

| Project | Purpose |
|---|---|
| `TsiBroker.Core` | Domain entities, JSON-file stores, messaging abstractions (`Messaging/`) — shared by all backend projects |
| `TsiBroker.ApiService` | Admin REST API + cookie auth, consumed by `TsiBroker.Ui` (Infrastructure Operators, Railway Undertakings management) |
| `TsiBroker.Ru.Api` | REST API exposed to Railway Undertakings, authenticated via `X-Api-Key` |
| `TsiBroker.Im.Api` | SOAP endpoints exposed to Infrastructure Managers (CoreWCF) |
| `TsiBroker.Ui` | Vue 3 admin frontend |
| `TsiBroker.AppHost` | Aspire orchestration of the above for local dev |

## Critical Workflow Rules

### 1. Wiki Maintenance (MANDATORY)

Before every commit, check whether the `wiki/` folder needs updating:

- New features / business rules → update the relevant wiki pages
- Architecture changes → `wiki/Architecture.md`
- New conventions or patterns → `wiki/Backend-Best-Practices.md`
- Frontend changes → `wiki/Frontend-Architecture.md`
- Data model changes → `wiki/Data-Model.md`

### 2. Language Convention

- All code, comments, documentation: **English**
- Commit messages: **English** (conventional commits)
- Exception: user-facing UI text is localized (`src/TsiBroker.Ui/src/locales/de.json`, `en.json`)
- User communication: **German** (the user speaks German)

### 3. Git

- Never push to a remote unless explicitly asked.
- Commit or push only when requested; branch first if on the default branch (`main`).

## Quick Reference Commands

### Development
```bash
# Start everything with Aspire (recommended)
dotnet run --project src/TsiBroker.AppHost

# Start UI standalone
cd src/TsiBroker.Ui && npm install && npm run dev

# Start ApiService standalone (needs Cors:AllowedOrigin for the UI dev server)
Cors__AllowedOrigin=http://localhost:5173 dotnet run --project src/TsiBroker.ApiService --launch-profile https
```

### Build
```bash
dotnet build TsiBroker.slnx
```

### Frontend
```bash
cd src/TsiBroker.Ui
npm run type-check   # vue-tsc
npm run lint         # oxlint + eslint --fix
npm run test:unit    # vitest
npm run build        # type-check + vite build
```

### Containers (local stack: api, ui, rabbitmq)
```bash
cd infrastructure
podman compose --env-file .env.example up -d --build
```

> There is currently no `.NET` test project in the solution — don't assume one exists when suggesting a test command; check `TsiBroker.slnx` first.

## Architecture Rules

### Backend

- **Feature folders, not layers**: each feature (`Auth/`, `InfrastructureOperators/`, `RailwayUndertakings/`) lives together — entity/store in `TsiBroker.Core`, endpoint mapping (`Map...Endpoints` extension methods on `IEndpointRouteBuilder`) in the hosting project (`TsiBroker.ApiService`, `TsiBroker.Ru.Api`)
- **Minimal APIs only** — no MVC controllers
- **No Repository/DbContext pattern** — stores are plain classes over JSON files (`*Store.cs` in `TsiBroker.Core`), registered as singletons, options bound via `IOptions<T>` with a `SectionName` const
- **No multi-tenancy** — this is a single-tenant broker; don't introduce `TenantId` filtering
- Keep SOAP/WCF contract code (`TsiBroker.Im.Api/CI`, `Heartbeat`) generated-code-adjacent but hand-written service logic in sibling files (`*MessageService.cs`), not inside `Generated/`
- Domain records for cross-cutting messages (e.g. `BrokerMessage`) go in `TsiBroker.Core/Messaging/`; publish via `IMessagePublisher`, never call RabbitMQ directly from feature code

### Frontend (Vue 3)

- **No generated API client** — calls go through `src/lib/api.ts` (`apiFetch`, `ApiError`); don't introduce a codegen step unless asked
- **All user-facing text MUST use i18n**: `$t('key')` / `t('key')` — never hardcode strings; add keys to both `locales/de.json` and `locales/en.json`
- **Composables** (`src/composables/`) for reusable stateful logic (e.g. `useLocale`, `useSidebar`)
- **Pinia stores** (`src/stores/`): client-side state only (currently `auth`)
- **Styling**: Less, under `src/assets/styles/` (`tokens.less` for variables) — no Vuetify/component library
- Auth cookie is `credentials: 'include'` on every `apiFetch` call — don't bypass it with a manual `fetch`

## Test Users (local dev / docker-compose)

| Username | Password | Source |
|---|---|---|
| `sa` | `temp` | `TsiBroker.ApiService/appsettings.json` (`AdminUser` section) |
| `sa` | `temp!` (default) | `infrastructure/.env.example` (`ADMIN_USERNAME`/`ADMIN_PASSWORD`, overridable per env file) |

Railway Undertaking and Infrastructure Operator API keys are generated at runtime per record (`ApiKeyEvuToBroker`, `ApiKeyBrokerToEvu`) — there are no fixed test keys; create/regenerate them via the admin API or UI.

## API Authentication (Testing)

```bash
# Admin API (cookie auth) — login, keep cookies for subsequent calls
curl -sk -c cookies.txt -X POST https://localhost:7261/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"sa","password":"temp"}'

curl -sk -b cookies.txt https://localhost:7261/api/railway-undertakings

# RU API (API key auth)
curl -H "X-Api-Key: <key>" http://localhost:5299/api/...
```
