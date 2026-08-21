# Handbuch: TSI-Broker Admin-Oberfläche

Dieses Handbuch richtet sich an Administratoren/Operatoren, die den TSI-Broker über die Weboberfläche verwalten. Der TSI-Broker vermittelt TAF/TAP-TSI-Nachrichten zwischen Eisenbahnverkehrsunternehmen (EVU) und Infrastrukturbetreibern (ISB). In dieser Oberfläche werden die Stammdaten beider Seiten gepflegt: welche EVUs und ISBs existieren, welche Nachrichtentypen sie miteinander austauschen dürfen, welche Zertifikate/API-Keys gültig sind, und wie es um die Zustellwarteschlangen steht.

## Inhalt

1. [Anmeldung](#1-anmeldung)
2. [Navigation](#2-navigation)
3. [Startseite](#3-startseite)
4. [Eisenbahnverkehrsunternehmen (EVU)](#4-eisenbahnverkehrsunternehmen-evu)
5. [Infrastrukturbetreiber (ISB)](#5-infrastrukturbetreiber-isb)
6. [Zertifikate eines Infrastrukturbetreibers](#6-zertifikate-eines-infrastrukturbetreibers)
7. [Queues](#7-queues)
8. [Allgemeine Bedienhinweise](#8-allgemeine-bedienhinweise)

---

## 1. Anmeldung

Beim Aufruf der Anwendung erscheint zunächst die Login-Seite mit dem TSI-Broker-Logo.

**Eingabefelder:**
- **Username** – Ihr Benutzername (Pflichtfeld)
- **Password** – Ihr Passwort (Pflichtfeld). Über das Augen-Symbol im Feld können Sie die Eingabe sichtbar machen ("Passwort anzeigen"/"Passwort verbergen").

Mit **„Login"** melden Sie sich an. Der Button wird während der Anmeldung deaktiviert.

- Bei falschen Zugangsdaten erscheint die Meldung *„Benutzername oder Passwort ist falsch."*
- Bei einem sonstigen Fehler (z. B. Server nicht erreichbar) erscheint *„Login fehlgeschlagen."*

Es gibt keine Selbstregistrierung und keine „Passwort vergessen"-Funktion – wenden Sie sich bei Zugangsproblemen an die Systembetreuung. Nach erfolgreicher Anmeldung werden Sie zur zuvor angeforderten Seite oder zur Startseite weitergeleitet. Ohne Aktivität meldet Sie das System nach einer gewissen Zeit automatisch ab (Session-Cookie mit gleitender Ablaufzeit); Sie müssen sich dann erneut anmelden.

## 2. Navigation

Nach der Anmeldung sehen Sie das Standard-Layout mit Seitenleiste (links) und Kopfleiste (oben).

**Seitenleiste** – über das Menü-Symbol lässt sie sich ein- und ausklappen:
| Menüpunkt | Zweck |
|---|---|
| **Startseite** | Begrüßungsseite |
| **Eisenbahnverkehrsunternehmen** | Verwaltung der EVU-Stammdaten |
| **Infrastrukturbetreiber** | Verwaltung der ISB-Stammdaten |
| **Queues** | Überwachung der Zustellwarteschlangen |

**Fußbereich der Seitenleiste:** Klick auf Ihren Benutzernamen öffnet ein Menü mit Sprachumschalter (Deutsch/Englisch) und dem Button **„Logout"**, mit dem Sie sich abmelden.

## 3. Startseite

Die Startseite (`/`) zeigt lediglich eine Begrüßung *„Willkommen beim TSI-Broker"*. Sie dient als Ausgangspunkt nach dem Login und hat keine weiteren Funktionen.

## 4. Eisenbahnverkehrsunternehmen (EVU)

### 4.1 Liste

Über den Menüpunkt **„Eisenbahnverkehrsunternehmen"** gelangen Sie zu einer Tabelle aller angebundenen EVUs mit den Spalten **Name**, **RicsCodes**, **SystemUrl**, **ISBs** (verknüpfte Infrastrukturbetreiber) und **Status** (grüner Badge „Aktiv" bzw. grauer Badge „Inaktiv").

- Ein **Doppelklick** auf eine Zeile öffnet die Detailansicht zum Bearbeiten.
- Über den Button **„+ Neues Eisenbahnverkehrsunternehmen"** öffnen Sie den Anlage-Dialog.
- Ist die Liste leer, erscheint der Hinweis *„Keine Eisenbahnverkehrsunternehmen vorhanden."*

### 4.2 Neu anlegen

Im Anlage-Dialog **„Neues Eisenbahnverkehrsunternehmen"** tragen Sie ein:

- **Name** (Pflichtfeld)
- **RicsCodes**: eine Liste von RICS-Codes, da ein EVU mehrere Codes haben kann. Über **„+ RicsCode hinzufügen"** fügen Sie weitere Felder hinzu, über das Mülleimer-Symbol entfernen Sie eines wieder (mindestens ein Feld bleibt immer bestehen). Leere Einträge werden beim Speichern automatisch entfernt.
- **SystemUrl** (Pflichtfeld) – die Adresse des Systems, unter der der Broker das EVU erreicht.

API-Keys und Verknüpfungen zu Infrastrukturbetreibern werden hier noch **nicht** vergeben – das neue EVU wird zunächst ohne diese Angaben angelegt. Nach dem Speichern springt die Anwendung automatisch in die Detailansicht des neuen EVU, damit Sie API-Keys und Verknüpfungen direkt ergänzen können.

Ist der eingegebene RICS-Code bereits einem anderen EVU zugeordnet, erscheint die Meldung *„RicsCode „{Code}" wird bereits von einem anderen Eisenbahnverkehrsunternehmen verwendet."*

### 4.3 Detailansicht / Bearbeiten

Die Detailansicht (`/railway-undertakings/:id`) ist die umfangreichste Seite der Anwendung. In der Kopfleiste finden Sie den Namen des EVU, einen Rücksprung-Link **„← Eisenbahnverkehrsunternehmen"** zur Liste sowie folgende Aktionen:

- **Löschen** (Mülleimer-Symbol) – nur sichtbar, wenn das EVU aktuell **gesperrt/inaktiv** ist. Ein aktives EVU muss also zunächst gesperrt werden, bevor es gelöscht werden kann. Es erscheint eine Sicherheitsabfrage *„„{Name}" wirklich löschen?"*.
- **Sperren/Entsperren** (Power-Symbol) – schaltet den Aktiv-Status um. Diese Änderung wird zunächst nur vorgemerkt (siehe Speicherverhalten unten).
- **„Konfigurationsupdate an EVU senden"** – löst **sofort**, unabhängig vom Speichern-Button, eine Benachrichtigung an das EVU-System aus. Während der Übertragung zeigt der Button *„Wird gesendet …"*; anschließend erscheint für einige Sekunden entweder *„Konfigurationsupdate wurde erfolgreich an das EVU gesendet."* oder eine Fehlermeldung.
- **„Speichern"** – speichert **alle** vorgemerkten Änderungen der gesamten Seite in einem Schritt (siehe unten).

Wurde der Aktiv-Status geändert, aber noch nicht gespeichert, erscheint der Hinweis *„Status-Änderung ({Entsperrt/Gesperrt}) wird beim Speichern übernommen."*

#### Stammdaten

Karte **„Stammdaten"** mit **Name**, **RicsCodes** (wie oben, dynamische Liste) und **SystemUrl**.

#### API-Keys

Karte **„API-Keys"** mit zwei Schlüsseln:

- **API-Key (EVU → Broker)** – damit authentifiziert sich das EVU-System beim Broker (eingehende Nachrichten).
- **API-Key (Broker → EVU)** – damit authentifiziert sich der Broker beim EVU-System (ausgehende Nachrichten).

Zu jedem Key stehen folgende Aktionen zur Verfügung:

- **Augen-Symbol** – Key ein-/ausblenden (standardmäßig maskiert dargestellt).
- **Kopieren-Symbol** – kopiert den aktuellen Key in die Zwischenablage; kurzzeitig erscheint *„Kopiert!"*.
- **„Neu generieren"** – erzeugt serverseitig einen neuen zufälligen Key. Der neue Key wird **nicht sofort aktiv**, sondern nur vorgemerkt und automatisch sichtbar gemacht, damit Sie ihn notieren/kopieren können. Solange die Änderung nicht gespeichert ist, erscheint der Hinweis: *„Wird beim Speichern übernommen. Der bisherige Key wird dann ungültig."*

> **Wichtig:** Notieren oder kopieren Sie einen neu generierten Key **vor** dem Verlassen der Seite bzw. vor dem Speichern – nach dem Neuladen der Seite ist der volle Wert nicht mehr im Klartext abrufbar (nur noch maskiert).

#### Verknüpfte Infrastrukturbetreiber

Karte **„Verknüpfte Infrastrukturbetreiber"** – hier legen Sie fest, mit welchen Infrastrukturbetreibern dieses EVU Nachrichten austauschen darf, und welche Nachrichtentypen dabei in welche Richtung erlaubt sind.

Tabelle mit Spalten **Name**, **RicsCode**, **„Nachrichten Senden"** (EVU → Broker) und **„Nachrichten Empfangen"** (Broker → EVU).

- **„+ Verknüpfung hinzufügen"** öffnet den Zuordnungsdialog zum Anlegen einer neuen Verknüpfung.
- **Doppelklick** auf eine Zeile öffnet denselben Dialog zum Bearbeiten.
- **Aktivieren/Deaktivieren** (Power-Symbol) je Zeile.
- **Löschen** (Mülleimer-Symbol) je Zeile – nur möglich, wenn die Verknüpfung zuvor deaktiviert wurde. Sicherheitsabfrage: *„Verknüpfung wirklich löschen?"*

**Zuordnungsdialog:**

- **„Infrastrukturbetreiber"** – Auswahlliste (Format „Name (RicsCode)"). Im Bearbeiten-Modus ist dieses Feld gesperrt; um den zugeordneten Infrastrukturbetreiber zu wechseln, muss die Verknüpfung gelöscht und neu angelegt werden.
- **„Nachrichten Senden (EVU → Broker)"** – Liste erlaubter Nachrichtentyp-Codes, z. B. `3003`. Mehrere Werte werden kommagetrennt eingegeben; `*` bedeutet „alle erlaubt".
- **„Nachrichten Empfangen (Broker → EVU)"** – analog für die Gegenrichtung.

Fehlt die Auswahl eines Infrastrukturbetreibers, erscheint *„Bitte einen Infrastrukturbetreiber auswählen."* Ist der gewählte Betreiber bereits verknüpft, erscheint *„Dieser Infrastrukturbetreiber ist bereits verknüpft."*

> Der Dialog speichert die Verknüpfung zunächst nur **lokal in der Seite** – erst der übergeordnete Button **„Speichern"** in der Kopfleiste überträgt alle Änderungen gemeinsam.

#### Speicherverhalten der Detailseite

Diese Seite arbeitet konsequent mit **vorgemerkten Änderungen**: Stammdaten, ein neu generierter API-Key, der Sperr-Status sowie sämtliche Änderungen an den Verknüpfungen (Hinzufügen, Bearbeiten, Aktivieren/Deaktivieren, Löschen) werden erst durch einen Klick auf **„Speichern"** tatsächlich übertragen. Solange Sie nicht speichern, können Sie die Seite verlassen – die Änderungen gehen dann verloren (kein automatisches Speichern). Einzige Ausnahme ist der Button „Konfigurationsupdate an EVU senden", der sofort wirkt.

## 5. Infrastrukturbetreiber (ISB)

Über den Menüpunkt **„Infrastrukturbetreiber"** gelangen Sie zu einer Tabelle mit den Spalten **Name**, **RicsCode**, **SystemUrl**, **Status** und einer Aktionsspalte.

- **Doppelklick** auf eine Zeile öffnet den Bearbeiten-Dialog.
- **„+ Neuer Infrastrukturbetreiber"** öffnet den Anlage-Dialog.
- Icon **„Zertifikate verwalten"** (Schild-Symbol) in der Aktionsspalte führt zur Zertifikatsverwaltung dieses Betreibers (siehe Kapitel 6).

**Anlage-/Bearbeiten-Dialog:**

- **Name**, **RicsCode**, **SystemUrl** (alle Pflichtfelder).
- Im Bearbeiten-Modus zusätzlich:
  - **Löschen** (Mülleimer) – nur sichtbar, wenn der Betreiber bereits **inaktiv** ist. Sicherheitsabfrage: *„„{Name}" wirklich löschen?"*
  - **Aktivieren/Deaktivieren** (Power-Symbol) – Status wird vorgemerkt, nicht sofort gespeichert.
- Buttons **„Abbrechen"** und **„Speichern"**.

Wie bei den EVUs wird eine Statusänderung erst beim Speichern wirksam (Hinweistext *„Status-Änderung ({Status}) wird beim Speichern übernommen."*); technisch löst das Speichern ggf. zwei Anfragen aus (Stammdaten + Statusänderung), was Sie als Operator aber nicht bemerken.

Ist der RICS-Code bereits vergeben, erscheint *„RicsCode „{Code}" wird bereits von einem anderen Infrastrukturbetreiber verwendet."*

## 6. Zertifikate eines Infrastrukturbetreibers

Diese Seite erreichen Sie nur über das Zertifikat-Symbol in der Infrastrukturbetreiber-Liste, nicht direkt über die Seitenleiste. Sie verwaltet die Zertifikate und die erwartete Identität der Gegenstelle für die gesicherte Verbindung zwischen Broker und diesem Infrastrukturbetreiber.

In der Kopfleiste: Name des Betreibers, Rücksprung-Link **„← Infrastrukturbetreiber"**, sowie – falls bereits Zertifikate hinterlegt sind – der Button **„Alle Zertifikate löschen"** (Sicherheitsabfrage: *„Wirklich alle Zertifikate für diesen Infrastrukturbetreiber löschen?"*). Es lässt sich nur das **gesamte** Zertifikats-Paket auf einmal löschen, nicht ein einzelnes Zertifikat.

### Drei Upload-Bereiche

1. **„Eigenes Client-Zertifikat"** – wird beim Verbindungsaufbau zum System dieses Infrastrukturbetreibers als Ausweis des Brokers gesendet (Datei-Typ: `.pfx`/`.p12`, enthält den privaten Schlüssel).
2. **„Erwartete Server-CA"** – die Zertifizierungsstelle, die das Server-Zertifikat des Infrastrukturbetreibers ausgestellt haben muss, damit der Broker der Gegenstelle vertraut (`.cer`/`.crt`/`.pem`).
3. **„Erwartete Client-CA"** – die Zertifizierungsstelle, die das Client-Zertifikat des Infrastrukturbetreibers ausgestellt haben muss, wenn sich dessen System beim Broker meldet (`.cer`/`.crt`/`.pem`).

Jeder Bereich zeigt entweder **„Hinterlegt: {Dateiname}"** (grün) oder **„Noch kein Zertifikat hinterlegt."** (grau) sowie einen Button **„Datei wählen …"** bzw. **„Ersetzen"**. Eine ausgewählte Datei wird **sofort** hochgeladen – es gibt hierfür keinen separaten Speichern-Schritt.

Praktisch bedeutet das: Sind nur die serverseitigen Felder (Zertifikat 2) gepflegt, prüft nur der Broker die Gegenstelle („1-way"). Sind zusätzlich das eigene Client-Zertifikat und die erwartete Client-CA (Zertifikate 1 und 3) gepflegt, weisen sich beide Seiten gegenseitig aus („2-way"). Für eine vollständig abgesicherte Verbindung müssen in der Regel alle drei Bereiche befüllt sein.

Fehler beim Hochladen: *„Datei konnte nicht hochgeladen werden."*, ggf. genauer *„Die Datei ist kein gültiges Zertifikat."* oder *„Die Datei konnte nicht gelesen werden."*

### Erwartete Identität & Sperrliste

Eigene Karte mit folgenden Feldern, die – anders als die Datei-Uploads – erst nach Klick auf **„Speichern"** übernommen werden:

- **Erwarteter Common Name (Server-Zertifikat)**
- **Erwarteter Common Name (Client-Zertifikat)**
- **CRL-URL (für eingehendes Client-Zertifikat)** – Sperrlisten-Adresse zur Prüfung, ob das eingehende Client-Zertifikat gesperrt wurde.
- **CRL-URL (für ausgehendes Server-Zertifikat)** – Sperrlisten-Adresse zur Prüfung, ob das Server-Zertifikat der Gegenstelle gesperrt wurde.

Nach erfolgreichem Speichern erscheint kurz der Hinweis *„Änderungen wurden gespeichert."* Bei Fehlern: *„Änderungen konnten nicht gespeichert werden."*

## 7. Queues

Die Queues-Seite (`/queues`) zeigt den Zustand der Zustellwarteschlangen zwischen Broker und den angebundenen Systemen, getrennt in zwei Abschnitte:

- **„IM-Queues"** – eine Zeile je Infrastrukturbetreiber
- **„EVU-Queues"** – eine Zeile je Eisenbahnverkehrsunternehmen

Spalten je Zeile: Name des Partners, technischer **Queue**-Name, Anzahl **Nachrichten** in der Warteschlange, Anzahl **Fehlerhafte Nachrichten**, sowie ein Status-Badge:

| Status | Bedeutung |
|---|---|
| **Aktiv** (grün) | Die Warteschlange verarbeitet Nachrichten normal. |
| **Pausiert** (orange/gelb) | Die Verarbeitung wurde angehalten, z. B. weil das Partnersystem nicht erreichbar war oder wiederholt Fehler zurückgemeldet hat. Beim Überfahren mit der Maus wird ggf. der Pausengrund als Tooltip angezeigt. |
| **Inaktiv** (grau) | Weder aktiv noch pausiert – meist, weil der zugehörige Partner selbst deaktiviert ist. |

**Aktion „Jetzt neu starten"** erscheint nur bei pausierten Queues. Nachdem die Ursache der Störung behoben wurde (z. B. Zertifikat erneuert, System des Partners wieder erreichbar), klicken Sie diesen Button, um die Zustellung manuell wieder anzustoßen, ohne das gesamte System neu starten zu müssen. Während der Anfrage ist der Button deaktiviert; danach werden beide Listen automatisch aktualisiert.

Gibt es keine Einträge, erscheint *„Keine Queues vorhanden."*

## 8. Allgemeine Bedienhinweise

- **Status-Badges:** Grün = Aktiv, Grau = Inaktiv, Orange/Gelb = Pausiert (nur bei Queues).
- **Löschschutz:** Infrastrukturbetreiber, EVUs und einzelne Verknüpfungen lassen sich nur löschen, wenn sie zuvor deaktiviert wurden. So werden aktive Konfigurationen nicht versehentlich gelöscht.
- **Doppelklick** auf eine Tabellenzeile öffnet überall die Bearbeitungsansicht bzw. den Bearbeitungsdialog.
- **Vorgemerkte Änderungen:** Bei der EVU-Detailseite und teilweise bei Infrastrukturbetreibern werden Statusänderungen, neue API-Keys und Verknüpfungsänderungen erst durch einen expliziten Klick auf „Speichern" wirksam. Ausnahmen sind Zertifikats-Uploads, die Identitätsfelder der Zertifikatsseite und der Button „Konfigurationsupdate an EVU senden" – diese wirken sofort.
- **Sicherheitsabfragen** erscheinen bei jedem Löschvorgang (Infrastrukturbetreiber, EVU, Verknüpfung, Zertifikate) und müssen bestätigt werden.
- Bei EVUs sind **RicsCodes** eine Mehrfachliste (ein EVU kann mehrere RICS-Codes haben); bei Infrastrukturbetreibern ist es ein einzelnes Pflichtfeld.
