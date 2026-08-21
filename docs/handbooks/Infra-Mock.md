# Handbuch: TSI-Broker Infra-Mock

Der **Infra-Mock** ("TSI-Broker | Infra") ist eine eigenständige Testanwendung, die einen echten Infrastrukturbetreiber (ISB/IM) simuliert. Mit ihr können Entwickler und Tester den TSI-Broker gegen ein Infrastrukturbetreiber-System testen, ohne dass ein reales System zur Verfügung stehen muss. Der Mock kann in beide Richtungen agieren:

- Er kann selbst Common-Interface-Nachrichten **an den Broker senden** (spielt also die Rolle eines Infrastrukturbetreibers, der eine Meldung einliefert).
- Er **empfängt** Nachrichten, die der Broker an ihn zustellt (spielt also die Gegenstelle, an die der Broker weiterleitet), und kann konfiguriert werden, wie er darauf antwortet (ACK, NACK oder simulierter Fehler).

> **Hinweis:** Diese Anwendung ist komplett in englischer Sprache gehalten (keine Sprachumschaltung vorhanden). Die Bildschirmtexte werden in diesem Handbuch auf Deutsch erklärt; die exakten englischen Beschriftungen sind zur Orientierung in **Anführungszeichen** angegeben.

## Inhalt

1. [Anmeldung](#1-anmeldung)
2. [Navigation](#2-navigation)
3. [Gesendete Nachrichten (Sent messages)](#3-gesendete-nachrichten-sent-messages)
4. [Empfangene Nachrichten (Received messages)](#4-empfangene-nachrichten-received-messages)
5. [Antwortverhalten (Response settings)](#5-antwortverhalten-response-settings)
6. [Weitere Hinweise](#6-weitere-hinweise)

---

## 1. Anmeldung

Beim Öffnen der Anwendung erscheint die Login-Seite mit dem Titel **„TSI-Broker | Infra"**.

**Felder:**
- **„Username"** – Benutzername
- **„Password"** – Passwort, mit Augen-Symbol zum Ein-/Ausblenden der Eingabe

Button **„Sign in"** meldet Sie an (während der Anfrage deaktiviert).

- Bei falschen Zugangsdaten: *„Invalid username or password."* (Benutzername oder Passwort ist falsch)
- Bei sonstigem Fehler: *„Login failed."* (Login fehlgeschlagen)

Es existiert nur **ein** gemeinsames Testkonto für den gesamten Mock – keine Einzelbenutzer, keine Selbstregistrierung, kein „Passwort vergessen". Nach erfolgreicher Anmeldung landen Sie auf der zuvor angeforderten Seite oder auf „Sent messages".

## 2. Navigation

Nach der Anmeldung sehen Sie die Seitenleiste mit drei Menüpunkten:

| Menüpunkt | Zweck |
|---|---|
| **„Sent messages"** (Startseite, `/`) | Nachrichten, die der Mock selbst an den Broker gesendet hat |
| **„Received messages"** | Nachrichten, die der Broker an den Mock zugestellt hat |
| **„Response settings"** | Einstellung, wie der Mock auf eingehende Nachrichten antwortet |

Über das Menü-Symbol lässt sich die Seitenleiste ein-/ausklappen. Im Fußbereich befindet sich der Button **„Log out (Benutzername)"** zum Abmelden.

## 3. Gesendete Nachrichten (Sent messages)

Diese Seite (Startseite nach dem Login) dient dazu, eine Testnachricht **vom Mock an den echten Broker** zu senden (Common-Interface-Endpunkt `/ci`) und zeigt ein Protokoll aller bisher gesendeten Nachrichten samt Ergebnis.

### Neue Nachricht senden

Über den Button **„+ New message"** öffnen Sie den Dialog **„Send a message (Mock → Broker, /ci)"**.

**Felder:**
- **„Sender (RICS)"** – wird beim Öffnen mit dem konfigurierten Standard-RICS-Code des Mocks vorbelegt, kann aber geändert werden.
- **„Recipient (RICS)"** – RICS-Code des Empfängers (Freitext).
- **„Target URL"** – Zieladresse, an die gesendet wird; standardmäßig die `/ci`-Adresse des echten Brokers. Kann bei Bedarf auf eine andere Broker-Instanz umgestellt werden.
- **„Message type"** – Auswahlliste der verfügbaren Nachrichtenvorlagen:
  - `ChangeOfTrackMessage`
  - `DelayCauseMessageService`
  - `TrainRunningForcecastMessage`
  - `TrainRunningInformationMessage`
  - `TrainRunningInterruptionMessage`
  
  Ein Wechsel des Nachrichtentyps lädt automatisch die passende Vorlage. Über das Symbol **„Reset to template"** (Kreispfeil) wird die Vorlage erneut geladen und alle Eingaben verworfen.

**Automatisch erzeugte Eingabefelder:** Für den gewählten Nachrichtentyp erzeugt die Anwendung passende Eingabefelder aus der zugrunde liegenden XML-Vorlage (z. B. „Operational Train Number", „Start Date", „Primary Location Code" – abhängig vom Nachrichtentyp). Felder, die noch nicht ausgefüllt sind, zeigen einen grauen Platzhaltertext, der mit `REPLACE-WITH...` beginnt.

> **Wichtig:** Wird ein solches Feld nicht ausgefüllt, wird der Platzhaltertext (z. B. wörtlich `REPLACE-WITH-TRAIN-NUMBER`) unverändert in die gesendete Nachricht übernommen. Der Broker wird eine solche Nachricht in der Regel als ungültig zurückweisen. Füllen Sie daher alle grau markierten Felder mit sinnvollen Testwerten.

Das Feld für die **Message Identifier** wird automatisch mit einer eindeutigen Kennung vorbelegt (Format `MOCK-...`) und muss normalerweise nicht angepasst werden.

**Rohdaten bearbeiten:** Über das Stift-Symbol **„Edit payload"** im Dialogkopf öffnen Sie ein weiteres Fenster **„Payload"** mit einem Textfeld **„Message XML"**, in dem Sie die komplette XML-Nachricht frei bearbeiten können (z. B. um bewusst fehlerhafte Nachrichten für Testzwecke zu erzeugen). Änderungen hier und im Eingabeformular werden automatisch synchronisiert. Mit **„Done"** schließen Sie dieses Fenster wieder.

**Senden:** Mit **„Send"** wird die Nachricht tatsächlich an die „Target URL" übertragen. Der Dialog schließt sich bei erfolgreicher Übertragung automatisch, das Ergebnis erscheint danach in der Tabelle. Schlägt bereits die Übertragung an die Zielsystem-Adresse fehl, bleibt der Dialog geöffnet und zeigt eine Fehlermeldung wie *„Send failed: invalid payload."* oder *„Send failed: HTTP {Code}."*

### Protokoll „Sent messages"

Tabelle mit den Spalten **Time (UTC)**, **Id**, **Result**, **File** und Aktionen. Ist die Liste leer: *„No messages sent yet."* Die Liste aktualisiert sich automatisch alle 5 Sekunden.

- **Result**-Badge: grün = `ACK` (vom Broker akzeptiert), rot = `NACK`, `HttpError` oder `Unreachable` (Zielsystem nicht erreichbar).
- Klick auf den **Dateinamen** kopiert den gesendeten XML-Inhalt in die Zwischenablage.
- **Zeile aufklappen** (Pfeil-Symbol) zeigt Anfrage- und Antwort-XML nebeneinander, jeweils mit eigenem Kopieren-Button.
- **„Duplicate"** (nur hier verfügbar) öffnet den Sende-Dialog erneut, vorbefüllt mit den Werten dieser Nachricht (mit neuer, eindeutiger Kennung) – praktisch, um einen Test mit kleinen Anpassungen zu wiederholen.

## 4. Empfangene Nachrichten (Received messages)

Diese Seite zeigt alle Nachrichten, die der echte Broker **an den Mock** zugestellt hat (`/ci`-Endpunkt des Mocks). Es handelt sich um ein reines Protokoll – Sie können hier keine Nachricht manuell erzeugen, Einträge entstehen nur, wenn der Broker tatsächlich eine Nachricht sendet.

Kartentitel: **„Received messages (Broker → Mock, /ci)"**.

**Werkzeugleiste:**
- **„Refresh"** – lädt die Liste sofort neu (zusätzlich zur automatischen Aktualisierung alle 5 Sekunden).
- **„Clear log"** – löscht das **gesamte** Nachrichtenprotokoll. Vor dem Löschen erscheint die Sicherheitsabfrage *„Clear the whole message log (received and sent)? This cannot be undone."* (Das gesamte Nachrichtenprotokoll löschen – Empfangen und Gesendet? Dies kann nicht rückgängig gemacht werden.)

  > **Wichtig:** Diese Funktion leert **sowohl** das Protokoll der empfangenen **als auch** der gesendeten Nachrichten, obwohl der Button nur auf dieser Seite angeboten wird.

Tabelle mit denselben Spalten wie bei „Sent messages" (Time, Id, Result, File). Result-Badge grün bei `ACK`, rot bei `NACK` und weiteren Fehlerfällen.

- **„Reply"** (nur hier verfügbar): Öffnet den Sende-Dialog auf der Seite „Sent messages", vorbefüllt mit den Daten dieser empfangenen Nachricht (Sender und Empfänger vertauscht, relevante Felder wie Zugnummer/Datum/Ortscode übernommen). So lässt sich schnell eine Antwort auf eine empfangene Nachricht erstellen.

Der Mock beantwortet jede eingehende Nachricht automatisch entsprechend der Einstellung auf der Seite „Response settings" (siehe Kapitel 5).

## 5. Antwortverhalten (Response settings)

Auf dieser Seite legen Sie fest, wie der Mock auf **künftige** eingehende Nachrichten vom Broker antworten soll. Die Einstellung gilt global für den gesamten Mock (nicht pro Nachricht) und wirkt sich **nicht** auf bereits empfangene Nachrichten aus.

Kartentitel: **„Response behaviour (Broker → Mock, /ci)"**.

**Felder:**
- **„Mode"** – Auswahlliste:
  - **`Ack`** (Standard) – jede eingehende Nachricht wird positiv bestätigt (ACK).
  - **`Nack`** – jede eingehende Nachricht wird abgelehnt (NACK). Zum Testen, wie der Broker mit Ablehnungen umgeht.
  - **`HttpError`** – der Mock antwortet mit einem simulierten Serverfehler (HTTP 500), um einen Ausfall des Infrastrukturbetreiber-Systems zu simulieren.
- **„Delay (ms)"** – künstliche Verzögerung in Millisekunden, bevor der Mock antwortet (mindestens 0). Damit lässt sich das Zeitverhalten des Brokers bei einer langsamen Gegenstelle testen.

Mit **„Save"** übernehmen Sie die Einstellung. Ein kleiner Statustext zeigt **„saved"** bei Erfolg bzw. **„error"** bei einem Fehler an.

> **Wichtig:** Diese Einstellung wird **nicht dauerhaft gespeichert** – sie liegt nur im Arbeitsspeicher des Mock-Dienstes und wird beim Neustart des Dienstes automatisch auf den Standardwert (`Ack`, 0 ms) zurückgesetzt. Denken Sie außerdem daran, nach einem Negativtest (z. B. `Nack` oder `HttpError`) den Modus wieder auf `Ack` zurückzustellen – sonst bleibt der Mock „stecken" und beantwortet auch spätere, eigentlich unabhängige Tests fehlerhaft.

## 6. Weitere Hinweise

- **Kopieren:** In beiden Protokollen kann per Klick auf den Dateinamen der Rohinhalt einer Nachricht in die Zwischenablage kopiert werden; ein kurzer Hinweis „Copied!" bestätigt dies.
- **Automatische Aktualisierung:** Beide Nachrichtenlisten aktualisieren sich alle 5 Sekunden von selbst.
- **Dateiablage (nur für technisch versierte Tester mit Dateisystemzugriff):** Der Mock beobachtet zusätzlich einen Ordner auf dem Server. Wird dort eine XML-Datei abgelegt, sendet der Mock sie automatisch an den Broker – als Alternative zum Dialog „+ New message". Dieser Ordner wird bei einem Neustart des Mocks **nicht** geleert (anders als die Protokolle).
- **Heartbeat:** Der Mock beantwortet auch die technischen „Lebenszeichen"-Anfragen (Heartbeat) des Brokers automatisch im Hintergrund. Diese erscheinen in keinem Protokoll und sind für den normalen Testbetrieb nicht relevant.
