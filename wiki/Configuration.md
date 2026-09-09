# Configuration & Environment Variables

Every runtime knob in TsiBroker is an ASP.NET Core configuration value. The most
practical way to set one is an **environment variable**; the same value can also
live in `appsettings.json`, `appsettings.<Environment>.json`, `dotnet
user-secrets`, or command-line args (standard .NET precedence: later wins).

**Key syntax**: a JSON path like `RabbitMq:Password` becomes the env var
`RabbitMq__Password` (`:` → `__`, double underscore). All examples below use the
env-var form.

**Value formats**:

- `bool` → `true` / `false`
- `TimeSpan` → `[d.]hh:mm:ss` (e.g. `00:00:05`, `1.00:00:00` for a day)
- `enum` → the member name (e.g. `RabbitMq`, `Debug`)

---

## 1. Standard .NET / ASP.NET Core host

Applies to **every backend project** (`TsiBroker.ApiService`,
`TsiBroker.Ru.Api`, `TsiBroker.Im.Api`, `TsiBroker.Im.Mock`,
`TsiBroker.Ru.Mock`) and, where noted, the Aspire `TsiBroker.AppHost`.

| Variable | Default | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (`Development` via launch profiles) | Hosting environment; gates `MapOpenApi()` and `appsettings.<env>.json` |
| `DOTNET_ENVIRONMENT` | — | Same as above for the non-web `AppHost` process |
| `ASPNETCORE_URLS` | per `launchSettings.json` | Bound URLs, e.g. `https://+:7261;http://+:5270` |
| `ASPNETCORE_HTTP_PORTS` | — | HTTP port(s) shorthand; set to `8081` in `infrastructure/docker-compose.yml` |
| `ASPNETCORE_HTTPS_PORTS` | — | HTTPS port(s) shorthand |
| `Logging__LogLevel__Default` | `Information` | Global log level |
| `Logging__LogLevel__Microsoft.AspNetCore` | `Warning` | Framework log level (`Aspire.Hosting.Dcp` also `Warning` in AppHost) |
| `AllowedHosts` | `*` | Host filtering (`ApiService`, `Ru.Api`, `Im.Mock`, `Ru.Mock`) |

### Aspire AppHost only

Set in `src/TsiBroker.AppHost/Properties/launchSettings.json`; override if the
ports collide.

| Variable | Purpose |
|---|---|
| `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` | OTLP ingestion endpoint for the dashboard |
| `ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL` | Aspire resource service endpoint |

---

## 2. Admin authentication — `AdminUser`

Bound by `TsiBroker.ApiService`, `TsiBroker.Im.Mock`, `TsiBroker.Ru.Mock`
(cookie login for the admin/mock UIs).

| Variable | Default (`appsettings.json`) | Purpose |
|---|---|---|
| `AdminUser__Username` | `sa` | Single admin account username |
| `AdminUser__Password` | `temp` | Single admin account password |

In `docker-compose.yml` these are fed from `ADMIN_USERNAME` / `ADMIN_PASSWORD`
(see [§8](#8-docker-compose-env-file-variables)), default `sa` / `temp!`.

---

## 3. CORS — `Cors`

Bound by `TsiBroker.ApiService`, `TsiBroker.Im.Mock`, `TsiBroker.Ru.Mock`.
Required when the Vue dev server runs on a different origin than the API.

| Variable | Default | Purpose |
|---|---|---|
| `Cors__AllowedOrigin` | *(none — no cross-origin allowed)* | Single allowed browser origin, e.g. `http://localhost:5173`. Aspire sets this automatically from the paired Vite app's endpoint. |

---

## 4. Flat-file data stores

No database — master data is JSON under `App_Data/`. `DataDirectory` overrides
where that folder lives (point at a mounted volume in containers).

| Variable | Bound by | Default | Purpose |
|---|---|---|---|
| `InfrastructureOperators__DataDirectory` | `ApiService`, `Ru.Api`, `Im.Api` | `<ContentRoot>/App_Data` | Location of `infrastructure-operators.json` |
| `RailwayUndertakings__DataDirectory` | `ApiService`, `Ru.Api`, `Im.Api` | `<ContentRoot>/App_Data` | Location of `railway-undertakings.json` |
| `CertificateBundles__DataDirectory` | `ApiService`, `Im.Api` | `<ContentRoot>/App_Data` | Location of `certificate-bundles.json` + `certificates/` |

> The Aspire AppHost overrides the first two for `apiservice`, `RailwayUndertaking-api`
> and `InfrastructureManagement-api` so all three processes share one `App_Data`
> folder. The container `Dockerfile` sets `InfrastructureOperators__DataDirectory`
> and `RailwayUndertakings__DataDirectory` to `/app/data` (mounted volume).

### Partner certificates — `CertificateBundles`

| Variable | Bound by | Default | Purpose |
|---|---|---|---|
| `CertificateBundles__PfxPassword` | `ApiService`, `Im.Api` | *(none)* | PKCS#12 password applied when opening every stored client certificate. Kept out of JSON on purpose — set via env var or user-secrets. |
| `CertificateBundles__ExpiryWarningThresholdDays` | `ApiService` | `30` | A partner certificate expiring within this many days triggers a `CertificateExpiryMonitor` warning |

---

## 5. Broker → IM SOAP delivery — `InfrastructureOperatorDelivery`

Bound by `TsiBroker.ApiService` only (spec 2.3.2 ACK/NACK timing + resend).

| Variable | Type | Default | Purpose |
|---|---|---|---|
| `InfrastructureOperatorDelivery__DeliveryTimeout` | `TimeSpan` | `00:00:05` | Per-attempt wait for an ACK/NACK from the IM |
| `InfrastructureOperatorDelivery__MaxMessageAge` | `TimeSpan` | `1.00:00:00` (24 h) | How long a timing-out message keeps being resent before it is dead-lettered (placeholder pending business sign-off) |
| `InfrastructureOperatorDelivery__RetryDelay` | `TimeSpan` | `00:01:00` | Delay between resend attempts after a timeout/transport failure |

---

## 6. Messaging backend — `Messaging` / `RabbitMq`

`Messaging` is read by `TsiBroker.ApiService` (publisher + consumer),
`TsiBroker.Ru.Api` and `TsiBroker.Im.Api` (publisher only). `RabbitMq` is bound
only when `QueueType = RabbitMq`.

| Variable | Type | Default | Purpose |
|---|---|---|---|
| `Messaging__QueueType` | `enum` | `RabbitMq` | `RabbitMq` (real broker) or `Debug` (no-op / log-only, no relay, no broker needed) |
| `RabbitMq__HostName` | `string` | `localhost` | Broker host |
| `RabbitMq__Port` | `int` | `5672` | Broker port |
| `RabbitMq__UseTls` | `bool` | `false` | Connect via `amqps` instead of `amqp` |
| `RabbitMq__VirtualHost` | `string` | `/` | RabbitMQ virtual host |
| `RabbitMq__UserName` | `string` | `tsibroker` | Broker username |
| `RabbitMq__Password` | `string` | `devpassword123` | Broker password (override outside local dev) |
| `RabbitMq__QueueName` | `string` | `tsi-messages` | Queue for non-partitioned messages |
| `RabbitMq__MaxDeliveryAttempts` | `int` | `3` | In-place retries before a message is dead-lettered (`{queue}.dead-letter`) |
| `RabbitMq__RetryDelay` | `TimeSpan` | `00:00:02` | Delay between in-place retries |

> The RabbitMQ container itself (`infrastructure/docker-compose.yml`) reads the
> stock image vars `RABBITMQ_DEFAULT_USER` (`tsibroker`) and
> `RABBITMQ_DEFAULT_PASS` (`devpassword123`).

---

## 7. Mock services

### `TsiBroker.Im.Mock` (ISB test double)

| Variable | Default | Purpose |
|---|---|---|
| `CiClient__TargetUrl` | `https://localhost:7262/ci` | Default outbound target (normally `Im.Api`'s `/ci`) |
| `CiClient__SenderAlias` | `ISB-MOCK` | Sender alias on outgoing messages |
| `CiClient__DefaultSenderRics` | `8430` | Prefilled Sender (RICS) field in the UI |
| `MockMessageStore__DataDirectory` | `<ContentRoot>/App_Data` | Holds the `In/Out/Sent` folders |

### `TsiBroker.Ru.Mock` (EVU test double)

| Variable | Default | Purpose |
|---|---|---|
| `RuClient__TargetUrl` | `https://localhost:7263/message` | Default outbound target (normally `Ru.Api`'s `/message`) |
| `RuClient__WhoAmIUrl` | `https://localhost:7263/whoami` | Target for the WhoAmI config check |
| `RuClient__ApiKey` | *(empty)* | `X-Api-Key` sent on outbound sends — the `ApiKeyEvuToBroker` of the RU this mock plays |
| `RuClient__DefaultSenderRics` | `0080` | Prefilled Sender (RICS) field in the UI |
| `MockMessageStore__DataDirectory` | `<ContentRoot>/App_Data` | Holds the `In/Out/Sent` folders |

> Aspire's `AppHost` comment notes `CiClient__TargetUrl` / `RuClient__TargetUrl`
> are the vars to override if `Im.Api` / `Ru.Api` ever stop using fixed local-dev
> ports.

---

## 8. Frontend (Vite — **build-time only**)

Vite inlines `VITE_*` vars into the JS bundle at build time; they are **not**
readable at container runtime. Set them as build args / `.env.<mode>` files.
Sources: each app's `.env.example`, `src/TsiBroker.Ui/Dockerfile`.

| Variable | Apps | Default | Purpose |
|---|---|---|---|
| `VITE_API_BASE_URL` | `TsiBroker.Ui`, `TsiBroker.Im.Mock.UI`, `TsiBroker.Ru.Mock.UI` | *(empty → same-origin `/api`)* | Base URL of the backing API |
| `VITE_THEME_COLOR` | all three UIs | `#6f4295` (compose: `#23a8f2`) | Brand/primary color; also recolors the favicon |
| `VITE_DEFAULT_LOCALE` | `TsiBroker.Ui` only | `en` | Default UI language when no locale cookie is set (`de` / `en`); the mock UIs have no i18n |

---

## 9. docker-compose `--env-file` variables

`infrastructure/.env.example` / `.env.dev` (copy per environment). These are
**compose interpolation** vars, mapped onto container env / build args in
`docker-compose.yml`:

| Variable | Maps to | Default |
|---|---|---|
| `ADMIN_USERNAME` | `AdminUser__Username` on `api`, `im-mock`, `ru-mock` | `sa` |
| `ADMIN_PASSWORD` | `AdminUser__Password` on `api`, `im-mock`, `ru-mock` | `temp!` |
| `VITE_THEME_COLOR` | `ui` image build arg | `#23a8f2` |

---

## Quick recipes

```bash
# Run ApiService standalone against a UI dev server, no broker
Cors__AllowedOrigin=http://localhost:5173 \
Messaging__QueueType=Debug \
dotnet run --project src/TsiBroker.ApiService --launch-profile https

# Point the broker at a real RabbitMQ
RabbitMq__HostName=rabbit.internal \
RabbitMq__UserName=tsibroker \
RabbitMq__Password='<secret>' \
RabbitMq__UseTls=true \
dotnet run --project src/TsiBroker.ApiService

# Unlock stored partner certificates
CertificateBundles__PfxPassword='<pfx-secret>' dotnet run --project src/TsiBroker.Im.Api
```
