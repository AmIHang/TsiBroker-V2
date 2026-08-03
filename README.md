# TsiBroker

Message-Broker zwischen Eisenbahnverkehrsunternehmen (RU/EVU) und Infrastrukturbetreibern (IM/ISB) für TAF/TAP-TSI-Nachrichten (Common Interface, Heartbeat).

## Tech Stack

| Backend | Frontend |
|---------|----------|
| .NET 10 (Minimal APIs, Feature Folders) | Vue 3 + TypeScript |
| Microsoft Aspire (lokale Orchestrierung) | Pinia + vue-router |
| JSON-Dateien unter `App_Data/` (keine Datenbank) | vue-i18n (DE, EN) |
| SOAP/WCF via CoreWCF (IM-Seite) | Less (kein Vuetify, kein generierter API-Client) |
| REST + `X-Api-Key` (RU-Seite) | |
| RabbitMQ (provisioniert, noch nicht angebunden) | |

## Quick Start

```bash
# Voraussetzungen: .NET 10 SDK, Node.js 20+, Podman/Docker Desktop
dotnet workload install aspire  # einmalig

# Backend + alle Services starten (Aspire Dashboard, Apis, Vite-Dev-Server)
dotnet run --project src/TsiBroker.AppHost

# Frontend standalone starten
cd src/TsiBroker.Ui
npm install
npm run dev
```

## Services (lokal)

| Service | URL |
|---------|-----|
| Frontend | http://localhost:5173 |
| Admin-API (ApiService) | https://localhost:7261 |
| RU-API (Ru.Api) | https://localhost:7263 |
| IM-API / SOAP (Im.Api) | https://localhost:7262 |
| Aspire Dashboard | https://localhost:17288 |

## Test-User

| Rolle | Username | Passwort | Quelle |
|-------|----------|----------|--------|
| Admin | sa | temp | `TsiBroker.ApiService/appsettings.json` (`AdminUser`) |
| Admin (Docker-Compose) | sa | temp! (Default) | `infrastructure/.env.example` (überschreibbar pro Env-Datei) |

RU- und IM-API-Keys werden pro Datensatz zur Laufzeit generiert (`ApiKeyEvuToBroker`, `ApiKeyBrokerToEvu`) und über die Admin-API/UI erzeugt bzw. regeneriert.

## Häufige Commands

```bash
# Build
dotnet build TsiBroker.slnx

# Frontend
cd src/TsiBroker.Ui
npm run type-check   # vue-tsc
npm run lint         # oxlint + eslint --fix
npm run test:unit    # vitest
npm run build        # type-check + vite build

# Container-Stack (api, ui, rabbitmq)
cd infrastructure
podman compose --env-file .env.example up -d --build
```

> Es existiert aktuell kein `.NET`-Testprojekt in der Solution.

## Dokumentation

Die ausführliche Dokumentation befindet sich im [Wiki](wiki/Home.md):

- [Architecture](wiki/Architecture.md) - Solution-Layout, Projektverantwortlichkeiten, Feature-Folder-Konvention
- [Backend Best Practices](wiki/Backend-Best-Practices.md) - Coding Standards, Konventionen
- [Frontend Architecture](wiki/Frontend-Architecture.md) - Vue 3 Struktur der Admin-UI
- [Data Model](wiki/Data-Model.md) - Entities, JSON-Datei-Storage
- [Business Flow](wiki/Business-Flow.md) - End-to-End-Nachrichtenfluss (CI, Heartbeat), Autorisierungsregeln
- [External API Guide](wiki/External-API-Guide.md) - Integrationsleitfaden für RU (REST) und IM (SOAP)

## Bekannte Lücken

Früher Projektstand - vor tieferer Arbeit am Code relevant:

- **Kein Nachrichten-Relay**: `Ru.Api` und `Im.Api` nutzen aktuell `DebugMessagePublisher`, der Nachrichten nur loggt und verwirft. RabbitMQ ist provisioniert, aber nicht angebunden.
- **Keine Datenbank**: Stammdaten liegen als JSON-Dateien unter `App_Data/`, geschützt durch einen In-Process-Lock.
- **Kein `.NET`-Testprojekt** in der Solution.
- Nur `TsiBroker.ApiService` ist in `infrastructure/docker-compose.yml` containerisiert; `Ru.Api` und `Im.Api` haben noch keinen Docker-Deploy-Pfad.
