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
| RabbitMQ (Standard-Messaging-Backend, End-to-End-Zustellung in beide Richtungen verdrahtet) | |

## Quick Start

```bash
# Voraussetzungen: .NET 10 SDK, Node.js 20+, Podman/Docker Desktop
dotnet workload install aspire  # einmalig

# RabbitMQ starten (wird von AppHost nicht mitverwaltet, siehe Hinweis unten)
cd infrastructure && podman compose --env-file .env.example up -d rabbitmq && cd ..

# Backend + alle Services starten (Aspire Dashboard, Apis, Vite-Dev-Server)
dotnet run --project src/TsiBroker.AppHost

# Frontend standalone starten
cd src/TsiBroker.Ui
npm install
npm run dev
```

> `Messaging:QueueType` defaultet auf `RabbitMq` und wird von keinem Projekt auf `Debug` zurückgesetzt; `TsiBroker.AppHost` registriert aber keine RabbitMQ-Ressource. Ohne erreichbaren RabbitMQ-Broker (siehe oben) schlägt Publish/Consume fehl — alternativ `Messaging__QueueType=Debug` setzen, um ohne Broker zu arbeiten (dann werden Nachrichten nur geloggt, kein Relay).

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

# .NET-Tests
dotnet test TsiBroker.slnx

# Container-Stack (api, ui, rabbitmq; im-mock/ru-mock über --profile)
cd infrastructure
podman compose --env-file .env.example up -d --build
```

## Dokumentation

Die ausführliche Dokumentation befindet sich im [Wiki](wiki/Home.md):

- [Architecture](wiki/Architecture.md) - Solution-Layout, Projektverantwortlichkeiten, Feature-Folder-Konvention
- [Backend Best Practices](wiki/Backend-Best-Practices.md) - Coding Standards, Konventionen
- [Frontend Architecture](wiki/Frontend-Architecture.md) - Vue 3 Struktur der Admin-UI
- [Data Model](wiki/Data-Model.md) - Entities, JSON-Datei-Storage
- [Business Flow](wiki/Business-Flow.md) - End-to-End-Nachrichtenfluss (CI, Heartbeat), Autorisierungsregeln
- [External API Guide](wiki/External-API-Guide.md) - Integrationsleitfaden für RU (REST) und IM (SOAP)

Für Administratoren/Tester gibt es außerdem End-User-Handbücher unter [`docs/handbooks/`](docs/handbooks/):

- [Broker.md](docs/handbooks/Broker.md) - Admin-Oberfläche (`TsiBroker.Ui`)
- [EVU-Mock.md](docs/handbooks/EVU-Mock.md) - EVU/RU-Mock-Testanwendung (`TsiBroker.Ru.Mock.UI`)
- [Infra-Mock.md](docs/handbooks/Infra-Mock.md) - ISB/IM-Mock-Testanwendung (`TsiBroker.Im.Mock.UI`)

## Bekannte Lücken

Der Nachrichten-Relay (RU→Broker→IM und IM→Broker→RU) ist inzwischen End-to-End implementiert (RabbitMQ, per-Partition-Queues, Retry/Dead-Letter, Pause/Resume bei Nichterreichbarkeit — siehe [Business Flow](wiki/Business-Flow.md)). Verbleibende Lücken:

- **Keine Autorisierungsprüfung auf dem IM→Broker-Pfad**: Jeder IM kann aktuell jede aktive EVU adressieren (kein Äquivalent zu `TsiMessageAuthorizationService`).
- **Heartbeat-Nachrichten werden nie veröffentlicht**, nur geloggt und echoed — anders als Common-Interface-Nachrichten.
- **Keine Datenbank**: Stammdaten liegen als JSON-Dateien unter `App_Data/`, geschützt durch einen In-Process-Lock.
- Nur `TsiBroker.ApiService`, `TsiBroker.Ui`, `TsiBroker.Im.Mock` und `TsiBroker.Ru.Mock` sind in `infrastructure/docker-compose.yml` containerisiert; `Ru.Api` und `Im.Api` (die eigentlichen partnerseitigen Endpunkte) haben noch keinen Docker-Deploy-Pfad.
