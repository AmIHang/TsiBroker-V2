# External API Guide

This guide is for developers of external systems — Railway Undertaking (RU/EVU) systems and Infrastructure Manager (IM/ISB) systems — integrating with TsiBroker. It covers both APIs: the REST API for RUs and the SOAP API for IMs.

> **Before you integrate:** as of today, TsiBroker validates and authorizes incoming messages but does **not** relay them to the other side yet (`IMessagePublisher` only logs). See [[Business-Flow]] for the exact current state. This guide documents the contracts as they exist; the delivery half is still to come.

## Table of Contents

- [RU API (REST)](#ru-api-rest)
  - [Authentication](#ru-authentication)
  - [POST /message](#post-message)
  - [GET /whoami](#get-whoami)
- [IM API (SOAP)](#im-api-soap)
  - [Common Interface — /ci](#common-interface--ci)
  - [Heartbeat — /heartbeat](#heartbeat--heartbeat)
- [Admin: Provisioning RU and IM Records](#admin-provisioning-ru-and-im-records)

---

## RU API (REST)

Base project: `TsiBroker.Ru.Api`. Local dev: `http://localhost:5290` / `https://localhost:7263`.

### RU Authentication

Every request must carry the RU's inbound API key in the `X-Api-Key` header:

```
X-Api-Key: <ApiKeyEvuToBroker>
```

This key is generated and stored per `RailwayUndertaking` (`ApiKeyEvuToBroker`, a random 40-character string) by a TsiBroker admin via the admin UI/API — see [[Data-Model]]. There is no self-service key creation; ask your TsiBroker admin to create or look up your RU record.

There is no key rotation grace period today — regenerating a key via the admin UI invalidates the old one immediately once saved.

### POST /message

Submit a TAF/TAP-TSI message (raw XML) to the broker, addressed to an Infrastructure Manager.

**Request**

```
POST /message
X-Api-Key: <ApiKeyEvuToBroker>
Content-Type: application/xml

<YourMessageRoot>
  <MessageHeader>
    <MessageReference>
      <MessageType>...</MessageType>
      <MessageIdentifier>...</MessageIdentifier>
    </MessageReference>
    <Sender>...</Sender>
    <Recipient>...</Recipient>
  </MessageHeader>
  ...
</YourMessageRoot>
```

The broker reads `MessageHeader` (namespace-agnostic, matched by local element name) as the **first child element of the document root**:

| Element | Required | Meaning |
|---|---|---|
| `MessageHeader/MessageReference/MessageType` | yes | The TAF/TAP message type — must be allowed for your RU→ISB assignment (or the assignment must allow `"*"`) |
| `MessageHeader/MessageReference/MessageIdentifier` | no | Echoed back in the response and used as `BrokerMessage.Id` |
| `MessageHeader/Sender` | yes | Must match one of your RU's registered RICS codes |
| `MessageHeader/Recipient` | yes | Must match an active Infrastructure Operator's RICS code that your RU has an active assignment for |

**Success response — `202 Accepted`**

```json
{ "status": "ACK", "messageIdentifier": "<your MessageIdentifier>" }
```

**Error responses** — RFC 7807 `application/problem+json`, one of:

| Status | Title | Cause |
|---|---|---|
| 401 | Missing API key | `X-Api-Key` header absent or blank |
| 401 | Invalid API key | No active RU found for the supplied key |
| 400 | Invalid message | Body isn't well-formed XML, or `MessageHeader`/required fields missing |
| 403 | Sender mismatch | `Sender` doesn't match any RICS code registered to your RU |
| 400 | Unknown recipient | `Recipient` doesn't match a known, active Infrastructure Operator |
| 403 | EVU not authorized for this ISB | No active assignment between your RU and that operator |
| 403 | Message type not authorized | Your assignment doesn't allow this `MessageType` for that operator |

See [[Business-Flow]] for the full authorization sequence.

### GET /whoami

Self-service endpoint to check what your RU is currently authorized to do — useful for verifying your admin-configured assignments without submitting a real message.

**Request**

```
GET /whoami
X-Api-Key: <ApiKeyEvuToBroker>
```

**Response** — `200 OK`, `application/xml`:

```xml
<RailwayUndertaking>
  <Name>...</Name>
  <RicsCodes>
    <RicsCode>...</RicsCode>
  </RicsCodes>
  <InfrastructureOperators>
    <InfrastructureOperator>
      <Name>...</Name>
      <RicsCode>...</RicsCode>
      <AllowedMessageTypesEvuToBroker>
        <MessageType>...</MessageType>
      </AllowedMessageTypesEvuToBroker>
      <AllowedMessageTypesBrokerToEvu>
        <MessageType>...</MessageType>
      </AllowedMessageTypesBrokerToEvu>
    </InfrastructureOperator>
  </InfrastructureOperators>
</RailwayUndertaking>
```

Only **active** `IsbAssignment`s to **active** `InfrastructureOperator`s are listed. If the API key is missing, invalid, or belongs to an inactive RU, the endpoint still returns `200 OK` with an "unknown" placeholder (`Name = "<unknown>"`, empty lists) rather than an error — don't rely on the HTTP status to detect an invalid key here, check whether `Name` is `<unknown>`.

---

## IM API (SOAP)

Base project: `TsiBroker.Im.Api`. Local dev: `http://localhost:5280` / `https://localhost:7262`. CoreWCF-hosted, `BasicHttpBinding` with `BasicHttpSecurityMode.Transport` (HTTPS transport security only — **no message-level security and no authentication is configured** on either endpoint today). WSDL is available via HTTP/HTTPS GET on each endpoint URL (`?wsdl`).

### Common Interface — /ci

Service contract: `ICommonInterfaceMessageService`, operation `UICMessage`, namespace `http://uic.cc.org/UICMessage`.

**Request** (`CommonInterfaceRequest`, unwrapped message with SOAP headers):

| Field | Kind | Description |
|---|---|---|
| `messageIdentifier` | SOAP header | Correlates with the response; used as `BrokerMessage.Id` |
| `messageLiHost` | SOAP header | Used as the ACK/NACK response's recipient/`RemoteLIName` |
| `compressed`, `encrypted`, `signed` | SOAP headers | Flags, not currently interpreted by the broker |
| `UICMessage` body (`message`, `signature`, `senderAlias`, `encoding`) | body | `message` becomes `BrokerMessage.Content` |

**Response** (`CommonInterfaceResponse`, wrapped as `UICMessageResponse`): a single `return` element containing a synthesized `<LI_TechnicalAck>`:

```xml
<LI_TechnicalAck>
  <ResponseStatus>ACK</ResponseStatus>  <!-- or NACK on any server-side exception -->
  <AckIndentifier>...</AckIndentifier>   <!-- sic - typo preserved from the source schema -->
  <MessageReference>...</MessageReference>
  <Sender>broker</Sender>
  <Recipient>...</Recipient>
  <RemoteLIName>...</RemoteLIName>
  <RemoteLIInstanceNumber>...</RemoteLIInstanceNumber>
  <MessageTransportMechanism>SOAP</MessageTransportMechanism>
</LI_TechnicalAck>
```

`NACK` is returned for any exception on the broker side, including a failed publish — the response doesn't currently distinguish "your message was malformed" from "the broker had an internal problem."

**Known limitation:** the broker does not currently derive `Sender`/`Receiver` from your message or route it to a specific RU — see [[Business-Flow]] for details. There is also no per-IM authorization equivalent to the RU side's `IsbAssignment` check yet.

### Heartbeat — /heartbeat

Service contract: `IHeartbeatMessageService`, operation `UICHBMessage`, namespace `http://uic.cc.org/UICMessage`.

**Request** (`HeartbeatRequest`, wrapped as `UICHBMessage`): `Message` and `Properties`, both freeform `XmlElement`.

**Response** (`HeartbeatResponse`, wrapped as `UICHBMessageResponse`): a single `return` element with the fixed text `HEART_BEAT_WS_RECEIVED`.

This is a liveness check only — it's logged on the broker side but never turned into a routable message (see [[Business-Flow]]).

---

## Admin: Provisioning RU and IM Records

Before any RU or IM can use the APIs above, a TsiBroker admin must create the corresponding record via the admin UI (`TsiBroker.Ui`) or the underlying admin API (`TsiBroker.ApiService`, cookie-authenticated, not for external use):

- `InfrastructureOperator` — name, RICS code, system URL
- `RailwayUndertaking` — name, one or more RICS codes, system URL, and at least one `IsbAssignment` naming which message types may flow in each direction to a given operator

API keys (`ApiKeyEvuToBroker`, `ApiKeyBrokerToEvu`) are generated automatically when a `RailwayUndertaking` is created, and can be individually regenerated afterwards from the edit screen. See [[Data-Model]] for the full entity shapes.
