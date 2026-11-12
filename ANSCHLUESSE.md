# Anschlüsse

toter-fitness greift nie direkt auf andere Ressourcen zu. Alles, was es von außen braucht (Framework, Geld, Inventar, Handy, Meldungen, Aussehen, Ziel-Menü), steckt in wenigen Dateien:

| Datei | Wofür |
|---|---|
| `Server/Bridge.cs` | Framework, Spielerdaten, Job, Geld, Firmenkonto, Rechnung, Steuer, Items, Handy-Nachrichten |
| `Client/Bridge.cs` | Meldungen, Charakter geladen, Jobwechsel, Aussehen neu laden, Handy schließen |
| `Client/Telefon.cs` | Apps im Handy anmelden (`AppSetzen`, `AppSenden`) |
| `Client/ziel.lua` | Option „Coachen“ im Ziel-Menü (ox_target / qb-target) |
| `Shared/Config.cs` | Namen von Ressourcen, Konten, Events |

Standard ist **QBCore** mit **lb-phone**, für das die Apps nativ entwickelt sind. Für ein anderes Setup werden nur diese Stellen angepasst, der Rest der Ressource ruft immer dieselben Funktionen mit denselben Rückgabewerten auf. Danach neu bauen (siehe README).

## Server (`Server/Bridge.cs`)

### Framework und Spieler

| Funktion | Muss liefern | Standard (QBCore) |
|---|---|---|
| `Core()` | Framework-Objekt | `exports['qb-core']:GetCoreObject()`, Name in `Config.CoreRessource` |
| `QbSpieler(player)` | Spielerobjekt oder `null` | `Core().Functions.GetPlayer(source)` |
| `CitizenId(player)` | eindeutige ID des Charakters, `null` ohne geladenen Charakter | `PlayerData.citizenid` |
| `Charname(player)` | „Vorname Nachname“ | `PlayerData.charinfo` |
| `Geburtstag(player)` | Geburtsdatum als Text (für die Mitgliedskarte) | `charinfo.birthdate` |
| `JobVon(player)` | `JobInfo` mit `Name`, `Rang` (Zahl), `RangName`, `ImDienst`, `Chef` | `PlayerData.job`, Chef = `isboss` oder Rang ab `Config.RangChef` |
| `Melden(player, text, typ)` | Meldung am Bildschirm, `typ` = `success`, `error` oder `primary` | Client-Event `QBCore:Notify` |

### Geld

| Funktion | Muss tun | Standard |
|---|---|---|
| `Bezahlen(player, betrag, grund)` | Geld abziehen, `true` nur bei Erfolg | `Functions.RemoveMoney(Config.Konto, betrag)` |
| `Erstatten(player, betrag, grund)` | Geld zurückbuchen | `Functions.AddMoney(Config.Konto, betrag)` |
| `AufFirmenkonto(job, betrag, grund)` | Einnahmen aufs Konto des Gym-Jobs | `qb-banking` `AddMoney`, sonst `qb-management` `AddMoney` |
| `RechnungStellen(von, an, betrag, grund, job)` | Rechnung für Coaching und Kurse, `true` wenn bezahlt | zieht den Betrag ab, Steuer geht ab, Rest aufs Firmenkonto |
| `Steuersatz()` | Steuersatz als Anteil (`0.05` = 5 %) | zentrale Steuer über `Config.SteuerRessource`, sonst `Config.TrainingSteuer` |
| `SteuerAbfuehren(betrag, grund)` | Steueranteil an die Staatskasse | über `Config.SteuerRessource`, sonst nur ins Log |

`Config.RechnungMitZustimmung = true` lässt den Kunden die Rechnung erst im Spiel annehmen. Nutzt euer Server ein eigenes Rechnungs-Skript, in `RechnungStellen` dessen Rechnung erstellen und die Einstellung auf `false` stellen.

**Zentrale Steuer:** Hat euer Server eine Ressource, die Steuersätze verwaltet oder eine Staatskasse führt, tragt in `Shared/Config.cs` ein:

```csharp
public const string SteuerRessource = "meine_staatskasse";
public const string SteuerSatzExport = "GetTaxRate";       // bekommt Config.SteuerArt, gibt 5 oder 0.05 zurück
public const string SteuerEinzahlenExport = "AddTax";      // bekommt (betrag, grund)
```

Die Namen oben sind Beispiele, es zählen die Exports eurer Ressource. Bleiben die Felder leer, gilt `TrainingSteuer` (5 %) und die Steuer verlässt das Spiel.

### Items

| Funktion | Muss tun | Standard |
|---|---|---|
| `ItemAnlegen(name, label, gewicht, einzeln, text)` | Item beim Inventar bekannt machen | `Core().Functions.AddItem` |
| `ItemBenutzbar(name, aktion)` | Benutzen abfangen und `aktion(serverId, item)` aufrufen | `Core().Functions.CreateUseableItem` |
| `ItemGeben(player, name, menge, info)` | Item mit Daten geben, `true` bei Erfolg | `Functions.AddItem` |
| `ItemNehmen(player, name, menge, slot)` | Item entfernen, `true` bei Erfolg | `Functions.RemoveItem` |
| `ItemsVon(player)` | alle Items als Liste mit `name`, `slot`, `info` | `PlayerData.items` |
| `ItemAnzeige(player, name, art)` | Einblendung „hinzugefügt/entfernt“ | Client-Event `inventory:client:ItemBox` |

Items: `gymkarte` (Mitgliedskarte, einzeln, mit Daten), `proteinshake`, `vitamine`, `muskelbalsam`. Zeilen für die Item-Liste und Bilder liegen in `inventar/`.

Daten der Mitgliedskarte (`info`): `gym`, `gymId`, `name`, `geburtstag`, `citizenid`, `nummer`, `ablauf` (Unix-Sekunden), `gueltigBis` (Text).

### Handy-Nachrichten

| Funktion | Muss tun | Standard |
|---|---|---|
| `HandyNachricht(player, titel, text)` | Nachricht der App Fitness aufs Handy | lb-phone `SendNotification`, sonst qb-phone `CustomNotification`, sonst `Melden` |

## Client (`Client/Bridge.cs`)

| Funktion | Muss tun | Standard |
|---|---|---|
| `Hinweis(text, typ, dauer)` | Meldung am Bildschirm | Event `QBCore:Notify` |
| `HandyZu()` | Handy zuklappen (vor dem Matten legen und dem Kursstart) | lb-phone `ToggleOpen(false)` |
| `SpielerGeladenAnmelden(aktion)` | `aktion` nach der Charakterauswahl aufrufen | Event `QBCore:Client:OnPlayerLoaded` |
| `JobWechselAnmelden(aktion)` | `aktion` bei Job- oder Dienstwechsel aufrufen | Events `QBCore:Client:OnJobUpdate`, `QBCore:Client:SetDuty` |
| `SkinNeuLaden()` | gespeichertes Aussehen neu laden | `Config.SkinNeuLadenServerEvent` oder `Config.SkinNeuLadenClientEvent` |
| `ZielStatusMelden(darf)` | Option „Coachen“ ein- oder ausblenden | Event an `Client/ziel.lua` |
| `KleidungNummer`, `KleidungPakete` | Addon-Kleidung über den Paketnamen finden | FiveM-Natives, normalerweise nichts zu ändern |

## Handy (`Client/Telefon.cs`)

| Funktion | Muss tun | Standard |
|---|---|---|
| `AppSetzen(id, sichtbar, name, text, seite)` | App anmelden oder abmelden | `exports['lb-phone']:AddCustomApp` / `RemoveCustomApp` |
| `AppSenden(stats)` | neue Werte an die offene App schicken | `exports['lb-phone']:SendCustomAppMessage` |

Die Apps Fitness und GymDesk sind nativ für lb-phone gebaut. Sie werden über `AddCustomApp` angemeldet, holen ihre Daten über `fetchNui` von lb-phone, nehmen Hell oder Dunkel aus den Handy-Einstellungen (`settings`, `onSettingsChange`), fragen bei Abrechnen, Verwerfen, Absagen, Abbauen und Abmelden über das Pop-up von lb-phone (`components.setPopUp`) nach und empfangen neue Werte über `SendCustomAppMessage`.

Für ein anderes Handy: Die Seiten (`html/telefon/fitness.html`, `html/telefon/gymdesk.html`) laufen auch ohne lb-phone. Daten kommen dann per normalem NUI-Fetch, Rückfragen entfallen und Hell oder Dunkel kommt über `?theme=dark` in der Adresse.

## Ziel-Menü (`Client/ziel.lua`)

Die Option „Coachen“ am anderen Spieler. Unterstützt werden `ox_target` (`addGlobalPlayer`) und `qb-target` (`AddGlobalPlayer`). Für ein anderes Ziel-Menü nur diese Datei anpassen: Option anzeigen, solange das Event `toter-fitness:client:zielStatus` zuletzt `true` gemeldet hat, und beim Auswählen `TriggerServerEvent('toter-fitness:server:coachen', serverIdDesZiels)` senden.

## Einstellungen mit Bezug zu anderen Ressourcen (`Shared/Config.cs`)

| Einstellung | Bedeutung | Standard |
|---|---|---|
| `CoreRessource` | Framework-Ressource | `qb-core` |
| `Konto` | Konto, von dem Spieler zahlen | `bank` |
| `Telefon`, `AppId`, `DeskAppId` | Handy-Ressource und App-Namen | `lb-phone`, `toter-fitness`, `toter-gymdesk` |
| `RangTrainer`, `RangChef` | Rang-Nummern im Gym-Job | `1`, `2` |
| `SkinNeuLadenServerEvent`, `SkinNeuLadenClientEvent` | Aussehen neu laden | `qb-clothing:loadPlayerSkin`, leer |
| `SteuerRessource`, `SteuerSatzExport`, `SteuerEinzahlenExport`, `SteuerArt` | zentrale Steuer | leer (5 % aus `TrainingSteuer`) |
| `Zeitzone`, `ZeitzoneWindows` | Tageswechsel und Kurszeiten | `Europe/Berlin` |
| `FettKleidung`, `AthletischKoerper` | Kleidung aus einem Addon-Paket je nach Körpertyp | aus |

## Beispiele für andere Setups

Die Beispiele zeigen, an welcher Stelle angepasst wird. Prüft die genauen Export-Namen in der Doku eurer Ressource, die Versionen unterscheiden sich.

**Qbox (`qbx_core`):** Qbox bringt eine Kompatibilität für QBCore mit, oft reicht `CoreRessource = "qb-core"` so wie es ist. Sonst in `QbSpieler` `exports.qbx_core:GetPlayer(source)` verwenden, die Felder in `PlayerData` heißen gleich.

**ESX:** `Core()` gibt `exports['es_extended']:getSharedObject()` zurück, `QbSpieler` wird `ESX.GetPlayerFromId(source)`. Dann:
- `CitizenId`: `xPlayer.identifier`
- `Charname`: `xPlayer.getName()`
- `JobVon`: `xPlayer.job.name`, `xPlayer.job.grade`, `xPlayer.job.grade_label`. ESX kennt keinen Dienst, `ImDienst` dann immer `true`.
- `Bezahlen`: `xPlayer.getAccount('bank').money` prüfen, dann `xPlayer.removeAccountMoney('bank', betrag)`
- Firmenkonto über `esx_addonaccount` (`society_<job>`)
- Items über `xPlayer.addInventoryItem` / `removeInventoryItem`
- Meldungen über `esx:showNotification`, Charakter geladen über `esx:playerLoaded`, Jobwechsel über `esx:setJob`

**ox_inventory:** Items in `ox_inventory/data/items.lua` eintragen (dann `ItemAnlegen` leer lassen) und die Bilder nach `ox_inventory/web/images`. `ItemGeben`/`ItemNehmen` über `exports.ox_inventory:AddItem(source, name, menge, info)` / `RemoveItem(...)`, `ItemsVon` über `GetInventoryItems(source)`. Benutzen über die Item-Option `server.export` oder ein Event.

**Banking:** Für Renewed-Banking, okokBanking oder ein eigenes System in `AufFirmenkonto` den Export zum Einzahlen auf ein Firmen- bzw. Job-Konto aufrufen.

**Meldungen mit ox_lib:** In `Melden` und `Hinweis` statt `QBCore:Notify` das Event `ox_lib:notify` mit `{ title, description, type }` senden.

**Aussehen mit illenium-appearance:** `SkinNeuLadenServerEvent = ""` und `SkinNeuLadenClientEvent = "illenium-appearance:client:reloadSkin"`.

**Anderes Handy:** In `AppSetzen` die Seite über die App-Schnittstelle des Handys einbinden, in `AppSenden` neue Daten an die Seite schicken, in `HandyZu` das Handy schließen und in `HandyNachricht` dessen Benachrichtigung nutzen. Ohne Handy haben Spieler keinen Zugriff auf ihre Werte, das Menü im Spiel ist nur für Admins.

## Für andere Ressourcen

Exports von toter-fitness:

| Seite | Export | Rückgabe |
|---|---|---|
| Server | `GetStats(citizenid)` | Tabelle mit `kraft`, `ausdauer`, `flexibilitaet`, `kondition` (0 bis 100) oder `nil`, wenn der Spieler nicht online ist |
| Server | `Koerper(source)` | `"fett"`, `"athletisch"` oder `"normal"` |
| Server | `KannTragen(traegerSource, zielSource)` | `true`, wenn der Träger den anderen tragen kann |
| Client | `Koerper()` | Körpertyp des eigenen Charakters |
| Client | `KannTragen(zielServerId)` | `true`, wenn man den anderen tragen kann |

Beispiel in Lua: `local werte = exports['toter-fitness']:GetStats(citizenid)`.
