# toter-fitness

## Zweck
Fitness-System für QBCore. Admins setzen Trainingsstationen mit Props in die Welt. Spieler trainieren dort mit einem Minispiel ihre Werte Kraft, Ausdauer, Flexibilität und Kondition (0 bis 100). Der Charakter macht die Übung sichtbar für alle mit. Kostenlose Stationen geben bis +3, Studio-Stationen kosten $50 und geben bis +6.

## Status
Version 1.0.0 (erstes öffentliches Release, github.com/toter99/toter-fitness, MIT). Interne Stände bis 3.4.0 sind darin aufgegangen, Änderungen ab jetzt in CHANGELOG.md. Spieler nutzen nur die Handy-Apps, im Spiel gibt es nur das Admin-Menü (/fitnessadmin). Server-Events sind pro Spieler gedrosselt (Main.Gedrosselt). Autor und Projektseite stehen in Config.Autor/Projektseite, Hinweis beim Start in der Server-Konsole, kleiner Vermerk im Admin-Menü und in den Apps.

- **Training:** 8 Übungen mit Minispielen (ohne Erklärtext), Abbau, Effekte, Patzer, Unverwundbarkeit, Muskelkater, Supplements, Bestenliste, Verlauf der Werte (30 Tage).
- **Gyms:** Mitgliedskarten, Empfang, Rechte nach Rang-Nummer (0 Rezeption, 1 Trainer, 2 Chef), Chef-Bereich mit Preisen und Team.
- **Coaching:** nur über das Alt-Menü „Coachen“ (Client/ziel.lua, ox_target oder qb-target), läuft ohne Zeitlimit, bis der Trainer im GymDesk abrechnet oder verwirft. Trainer-Bonus +2 pro Übung (Config.TrainerPunkte), erst nach bezahlter Rechnung. Ruhige Coach-Animation (zuschauen, am Boden knien).
- **Kurse:** im GymDesk planen (Übung, Plätze, Preis, Dauer ab 15 Minuten, Ort automatisch), Matten selbst legen (drehen, Höhe, Bodenprüfung, später weiterlegen), gemeinsamer Start nach Handy weg, Kursleitung immer geschafft, Rechnung automatisch nach der Übung, Kurs-Bonus +3, Erinnerung 30 und 15 Minuten vorher, Liste wird an alle verteilt.
- **Steuer:** 5 % (Config.TrainingSteuer) oder zentrale Einstellung über Config.SteuerRessource, SteuerSatzExport, SteuerEinzahlenExport.
- **Handy:** Apps „Fitness“ und „GymDesk“, nativ fürs lb-phone entwickelt (Dunkelmodus aus den Handy-Einstellungen). Andere Handys über die Anschlüsse in Client/Telefon.cs (ANSCHLUESSE.md). Menü im Spiel nur für Admins, keine Chat-Befehle außer /fitnessadmin.
- **Sicherheit:** Geld, Rechte, Abstände und Preise prüft nur der Server. Jedes Server-Event ist pro Spieler gedrosselt (Main.Gedrosselt), SQL nur mit Parametern, Texte in den Oberflächen nur als Text (kein HTML aus Spielerdaten). Test-Befehle sind entfernt.
- **Technik:** Alles in C# bis auf Client/ziel.lua, fremde Ressourcen nur über die Anschlüsse (ANSCHLUESSE.md). Datenbank-Tabellen legt der Server selbst an.

## Übungen
| Wert | Frei | Studio | Minispiel |
|---|---|---|---|
| Kraft | Klimmzüge (Charakter hängt, zieht sich über 80 % hoch) | Bankdrücken | Leertaste schnell drücken, über 80 % halten, wird immer schwerer |
| Ausdauer | Joggen: Charakter läuft die gesetzte Strecke ab | Laufband | Dino-Lauf: springen und ducken, 3 Leben |
| Flexibilität | Yoga auf der Matte | Pilates-Kurs | Yoga (wie im Original-GTA): 3 Haltungen, je WASD und Pfeiltasten auf die Markierungen ausrichten, dann 3 Atemzüge im Takt mit der Leertaste. Pilates: Balance, Leertaste halten/loslassen, Nadel im grünen Bereich halten. Der Balken füllt sich nur im grünen Bereich und ist voll, wenn man rund 87 % der Zeit drin war |
| Kondition | Liegestütze | Zirkeltraining (Sit-ups) | Rhythmus: angezeigte Taste W/A/S/D rechtzeitig drücken |

Dauer pro Übung (Config.cs, Uebung.Dauer): Studio zusammen 10 Minuten (Bankdrücken 2:00, Laufband 3:00, Pilates 2:30, Zirkel 2:30), frei zusammen 15 Minuten (Klimmzüge 3:00, Joggen 4:30, Yoga 4:00, Liegestütze 3:30). Die Minispiele passen sich an: Kraft in Sätzen (25 s Arbeit, 10 s Pause, 12 Wiederholungen pro Satz als Ziel), Dino und Balance werden über die ganze Zeit schwerer, Yoga hat eine Haltung pro etwa 22 s, Rhythmus eine Taste pro etwa 1,75 s. Joggen läuft auf Zeit, die Strecke wird dabei wiederholt abgelaufen.

Jeder Wert kann einmal pro Tag trainiert werden (frei oder Studio), neuer Tag ab 4:00 Uhr Europe/Berlin (Config.EinmalProTag, TagesResetStunde). Abgebrochene Trainings zählen nicht, abgeschlossene und verpatzte schon. Im Admin-Bereich gibt „Heute alles freigeben“ die Werte eines Spielers wieder frei. In der App Fitness steht bei bereits trainierten Werten „heute trainiert“.

Einstellungen (Preise, Zugewinn, Abklingzeit, Props, Animationen) stehen in `Shared/Config.cs`.

## Menü
Spieler nutzen nur die Handy-Apps „Fitness“ und „GymDesk“. Das Menü im Spiel ist nur noch das Admin-Menü (`/fitnessadmin`, Unter-Reiter Stationen, Spieler, Gyms, Kurse, Bestenliste, Diagnose). Kein Radialmenü-Eintrag, keine Taste, kein `/fitness`.

## Handy-Apps
Oberflächen in html/telefon (gemeinsam.js, app.css), Client/Telefon.cs. Angelehnt an die Health-App, hell und dunkel, passen sich der Breite an.
- **Fitness** (fitness.html, alle): Ringe für die vier Werte, je Wert eine Karte mit Stufe und ob heute trainiert, Zustand (Muskelkater, Shake, Vitamine), Mitgliedschaften, Stationen in der Nähe mit „Route“ (Wegpunkt), Bestenliste je Übung, Trainingseinheiten.
- **GymDesk** (gymdesk.html, nur Mitarbeiter eines Gyms): Kopf mit Gym, Rang und Dienst. Reiter Training (Trainer im Dienst: Preis, Kunden in der Nähe, Anbieten, Abrechnen, Aktualisieren) und Studio (nur Chef: Preise Mitgliedschaft und Shop speichern, Team online).
- Daten holen die Seiten selbst per POST an `https://toter-fitness/<callback>`: `appDaten`, `appWegpunkt`, `appBestenliste`, `deskDaten`, `deskTraining` (aktion anbieten|abrechnen|liste), `deskPreise`. Der Client fragt den Server und antwortet nach höchstens 3 s.
- lb-phone: Fitness wird immer als Custom App angemeldet, GymDesk nur, solange der Server im Feld `studio` ein Gym meldet (Job eines Gyms). Bei Job- oder Dienstwechsel (QBCore:Client:OnJobUpdate, SetDuty) holt der Client neue Werte und meldet GymDesk an oder ab. Neue Werte nach dem Training gehen zusätzlich per `SendCustomAppMessage` an Fitness.
- **Verlauf:** Antippen einer Wert-Karte öffnet die Detailseite mit Kurve der letzten Config.VerlaufTage (30) Tage, höchstem und tiefstem Wert, Veränderung und Erklärung. Der Server schreibt bei jedem Speichern den Tagesstand in `fitness_verlauf` (citizenid, tag = Tagesbeginn 04:00, vier Werte), alte Zeilen werden beim Start gelöscht. Server-Event `verlauf` (stat), Client-Callback `appVerlauf`.
- **Kurse:** Die Rezeption (Rang 0) sieht den Reiter Kurse nur zum Lesen. Trainer und Chefs (ab Config.RangTrainer, Dienst nicht nötig) planen Name, Übung, Plätze (bis Config.KursMaxTeilnehmer 20), Tag (heute bis +6), Beginn, Dauer (15 bis 90 Minuten) und Preis pro Person (bis Config.TrainerMaxPreis, 0 = kostenlos). Der Kursort ist der Standort beim Planen, der Treffpunkt-Text (Straße, Gegend) wird daraus ermittelt, beim Lauf-Kurs am Anfang der Strecke. Strecken stehen in der Auswahl mit Länge und Gegend. Höchstens Config.KurseProGym gleichzeitig, Tabelle `fitness_kurse` (Anmeldungen als citizenid-Liste, Spalten uebung, max, richtung, route).
  - Übungen (Config.KursArten): Yoga-Kurs, Pilates-Kurs, Hantel-Kurs (Kurzhanteln auf der Matte als Ersatz für die Bank), Sit-up-Kurs, Liegestütz-Kurs (alle mit Matte) und Lauf-Kurs auf einer vorhandenen kostenlosen Laufstrecke (Auswahl der Strecken im Umkreis).
  - Matten legen: Jeder Mitarbeiter des Gyms (auch Rezeption) tippt im GymDesk „Matten legen“ (Client/KursMatten.cs). Das Handy klappt zu (lb-phone ToggleOpen), dann liegt eine Vorschau-Matte dort, wo man hinschaut. Mausrad oder Pfeil links/rechts dreht, der Pfeil am Boden zeigt die Blickrichtung beim Training. E legt hin, X nimmt die letzte zurück, Enter ist fertig, Rücktaste bricht ab. Die erste Matte ist die der Kursleitung (gold), danach bis zu Plätze-viele für die Teilnehmer (lila), die von sich aus zur Kursleitung schauen. Rot = geht hier nicht: kein Boden, zu schräg (Config.MatteMaxSchraeg 0.25 m unter der Matte), mehr als 2 m höher/tiefer, weiter als Config.KursRadius vom Kursort, näher als Config.MattenMinAbstand an einer anderen Matte. Die Matte liegt mit der Unterseite auf dem höchsten Bodenpunkt darunter, nichts steckt im Boden (gleiche Rechnung beim Erzeugen der Props). Server-Event `kursMatten` (id, Liste x/y/z/h). Spieler können die Matten nicht abbauen. Beim Lauf-Kurs gibt es weiter „Start aufstellen“ am Anfang der Strecke.
  - Ablauf: Nur Angemeldete können einen Platz einnehmen (Prompt an der Matte), sie warten dann. Die Leitung (oder der Chef) startet im GymDesk („Starten (n bereit)“), alle beginnen gleichzeitig. Beim Lauf-Kurs läuft die Leitung zuerst los, die Gruppe kurz danach. Teilnehmer bekommen den Studio-Wert ohne Mitgliedschaft und ohne Kosten.
  - Wer den Wert heute noch nicht trainiert hat, bekommt im Kurs den normalen Studio-Zugewinn plus Trainer-Bonus plus Config.KursBonus (3). Wer ihn heute schon trainiert hat, darf trotzdem mitmachen (Tageslimit und Muskelpause gelten im Kurs nicht) und bekommt nur die +3 (Sitzung.Nachschlag). Voraussetzung für die +3: Übung mindestens 25 % geschafft und Leitung in der Nähe.
  - Bonus und Preis: Wie beim Coaching legt der Kurs eine Buchung beim Trainer an, Trainer-Bonus und Kurs-Bonus liegen dort. Nach der Übung geht automatisch die Rechnung mit dem Kurspreis raus (KursAbrechnen). Bezahlt: Boni gutgeschrieben, Geld nach Steuern ans Gym. Abgelehnt: Boni entfallen. Kostenlos: Boni sofort. Pro Kurs und Person nur einmal (Kurs.Abgerechnet), eine zweite Runde zählt nicht als Kurs. Der Trainer-Bonus wird erst gutgeschrieben, wenn der Teilnehmer die Rechnung bezahlt oder der Trainer verwirft. Lehnt er die Rechnung ab, entfällt er.
  - Server-Events `kurse`, `kursAnlegen`, `kursAbsagen` (id), `kursAnmelden` (id, dabei), `kursAufbauen` (id, aufbauen), `kursStarten` (id).
- **Nachrichten:** HandyNachricht in Server/Bridge.cs (lb-phone: Server-Export SendNotification an die App Fitness, qb-phone: `qb-phone:client:CustomNotification`, sonst normale Meldung). Beim Einloggen: Mitgliedschaft läuft in 24 h ab, Muskelkater, ein Wert seit Config.ErinnernNachTagen (3) nicht trainiert, angemeldete Kurse heute. Kurse: Config.KursErinnerungen (30 und 15 Minuten) vorher, beim Start, bei Absage.
- Native lb-phone Apps: gemeinsam.js nutzt fetchNui (mit Ressourcenname), settings/getSettings/onSettingsChange fürs Thema und components.setPopUp für Rückfragen (frage(): Abrechnen, Verwerfen, Kurs absagen, Matten abbauen, vom Kurs abmelden). Ohne lb-phone: normales fetch, keine Rückfrage, Thema über ?theme=. Zurück und Esc regelt lb-phone selbst.

## Gyms und Mitgliedskarten
- Admins legen im Menü Gyms an (Name) und ordnen Studio-Stationen einem Gym zu.
- Studio-Station mit Gym: nur mit gültiger Mitgliedskarte dieses Gyms nutzbar, dafür ohne Kosten pro Training. Studio-Station ohne Gym: wie bisher Config.PreisStudio pro Training.
- Karte = Item `gymkarte` (unique) mit info {gym, gymId, name, geburtstag, citizenid, nummer, ablauf (Unix-Sekunden), gueltigBis}. Gültig nur für die Person, auf die sie ausgestellt ist.
- Ausstellen im Admin-Menü: Spieler-ID, Gym, Dauer in Minuten/Stunden/Tagen (60 s bis 365 Tage). Hat die Person schon eine Karte für das Gym, wird sie verlängert (gleiche Nummer, neues Ablaufdatum).
- Abgelaufene Karten entfernt der Server jede Minute aus den Inventaren der Spieler online (Config.KartenPruefenSekunden), mit Meldung.
- Karte benutzen: zeigt sie 8 Sekunden lang einem selbst und allen im Umkreis von 3 m (NUI, Daten aus dem Item).
- Das Item legt toter-fitness beim Start über QBCore.Functions.AddItem an. Bild und Ersatz-Eintrag für items.lua: `inventar/`.
- Tabelle `fitness_gyms` und Spalte `fitness_stations.gym_id` legt der Server beim Start selbst an.
- Die eigenen gültigen Mitgliedschaften stehen in der App Fitness.

## Gym-Empfang
- Pro Gym im Admin-Menü (Gyms → Einstellungen): Mitarbeiter-Job (QBCore-Jobname, z. B. `gym`), Preise für 1, 7 und 30 Tage, „Empfang hier setzen“ an der eigenen Position.
- Am Empfang (Marker mit $, E):
  - Mitarbeiter mit dem Job des Gyms, im Dienst (Config.EmpfangNurImDienst): Kunde in der Nähe (6 m) wählen, dann Laufzeit und „Angebot schicken“ oder bei einem Supplement „Anbieten“. Der Kunde bekommt ein Fenster mit Preis und 60 s Zeit (Config.AngebotSekunden). Annehmen: Geld vom Bankkonto des Kunden, Karte ausgestellt bzw. verlängert, Geld aufs Firmenkonto des Jobs (AufFirmenkonto in Server/Bridge.cs). Ist das Inventar voll, gibt es das Geld zurück.
  - Gym ohne Job: Selbstbedienung, man kauft Mitgliedschaft und Supplements direkt.
  - Alle anderen sehen die Preise und den Hinweis, einen Mitarbeiter anzusprechen.
- Admins können Karten weiterhin kostenlos im Admin-Menü ausstellen.

## Supplements
Items, die Angestellte am Empfang anbieten (Kunde in der Nähe auswählen, „Anbieten“, Kunde nimmt an und zahlt). Selbst kaufen geht nur in Gyms ohne Job. Preise pro Gym im Admin-Menü (Gyms → Einstellungen → Shop, 0 = nicht im Angebot), gespeichert in fitness_gyms.shop ("item=preis;…"), sonst Config.Supplements. Geld aufs Firmenkonto des Gym-Jobs.
- Protein-Shake (`proteinshake`): 2 Stunden +1 Punkt pro geschaffter Übung (Config.ShakePunkte).
- Vitamin-Kapseln (`vitamine`): 24 Stunden kein Abbau (die Zeit zählt nicht für den Abbau).
- Muskelbalsam (`muskelbalsam`): beendet den Muskelkater.
Wirkung läuft weiter, wenn man mehrere nimmt (Zeit wird angehängt). Gespeichert in player_fitness (buff_shake_bis, buff_vitamine_bis). Die aktiven Wirkungen stehen in der App Fitness.

## Personal Trainer
- Wer: Angestellte eines Gyms im Dienst mit Rang ab Config.RangTrainer (1). Ränge im Gym-Job: 0 Rezeption, 1 Trainer, 2 Chef.
- Start: Trainer schaut im Alt-Menü (ox_target, sonst qb-target) einen Spieler an, Option „Coachen“. Server-Event `coachen`. Der Spieler bekommt die Frage und nimmt an oder lehnt ab (Angebot "coaching", ohne Preis).
- Coaching: läuft ohne Zeitlimit, bis der Trainer abrechnet oder verwirft. Bei jeder Übung, bei der der Trainer beim Start in der Nähe ist, coacht er sie (Animation: knien bei Übungen am Boden, sonst mit verschränkten Armen zuschauen; beim Joggen keine). Ist er am Ende der Übung noch in der Nähe, werden +2 Punkte (Config.TrainerPunkte) in der Buchung vorgemerkt. Gutgeschrieben wird er erst, wenn der Kunde die Rechnung bezahlt oder der Trainer verwirft. Lehnt der Kunde ab, entfällt er.
- Ende: Wenn die Zeit um ist, steht das Coaching als „beendet“ beim Trainer (GymDesk). Laufende Coachings kann er jederzeit beenden.
- Abrechnen ist freiwillig: „Abrechnen“ mit Betrag schickt dem Kunden eine Rechnung (Angebot "rechnung", Config.RechnungMitZustimmung), er bezahlt oder lehnt ab. Bezahlt: RechnungStellen in Server/Bridge.cs bucht ab, Steuersatz() (5 %, sonst zentrale Einstellung) geht als Steuer ab (SteuerAbfuehren), der Rest aufs Firmenkonto. „Verwerfen“ beendet kostenlos. Nicht entschiedene Coachings verfallen nach Config.AbrechnenStunden (2) oder wenn der Trainer geht.
- Server-Events: `coachen` (kundeId), `trainingAbschliessen` (buchungId, betrag; 0 = verwerfen), `trainerListe`.
- Boni von Shake und Trainer addieren sich. Bruchteile werden als Chance auf einen Punkt mehr gewertet, nicht aufgerundet.

## Bestenliste
Jedes Minispiel liefert einen Rekordwert: Kraft Wiederholungen, Lauf-Spiel Meter, Balance % im Gleichgewicht, Yoga Punkte, Rhythmus Treffer. Bestwert pro Spieler und Übung in `fitness_rekorde` (citizenid, uebung "stat:art", wert, name, datum), begrenzt durch Config.RekordMaximum. App Fitness: Top 10 pro Übung und der eigene Rekord. Neuer persönlicher Rekord wird gemeldet.

## Muskelkater
Wer an einem Trainingstag alle vier Werte trainiert, hat ab dem nächsten Tageswechsel (4:00) 12 Stunden Muskelkater (Config.MuskelkaterStunden): Bewegung 88 % (Config.MuskelkaterTempo, SetPedMoveRateOverride jeden Frame). Ab einem Schnitt der vier Werte von 75 (Config.MuskelkaterSchutzAb) gibt es keinen. Muskelbalsam beendet ihn. Gespeichert in muskelkater_ab/muskelkater_bis.

## Platzieren
Admin-Menü → Station setzen → „Platzieren“ bzw. bei einer Station „Versetzen“: halbtransparenter Geister-Prop folgt dem Blick (höchstens 12 m). Mausrad oder Q/E drehen, Pfeil hoch/runter Höhe, Umschalt fein, Enter setzen, Rücktaste abbrechen. Blauer Kreis und Pfeil zeigen, wo der Charakter beim Training steht und wohin er schaut. Server-Aktion `platzieren` (höchstens 20 m vom Admin). „An meiner Position“ und „Hierher“ setzen wie bisher an die eigene Position.

## Befehle
Chat-Befehle gibt es nur `/fitnessadmin` (Admins). Die früheren Test-Befehle sind für das Release entfernt, alles Nötige liegt im Admin-Menü.

Admin-Rechte: ACE `command.fitnessplace` (QBCore-Admins haben es über `group.admin`).

## Schnittstellen
Server-Events (`toter-fitness:server:…`, alle gedrosselt und auf dem Server geprüft):
- Training: `stationenHolen`, `statsHolen` (oeffnen), `start` (stationId), `ergebnis` (token, punkte 0–100, rekord), `abbruch` (token), `verlauf` (stat), `bestenliste` (uebung)
- Gyms und Empfang: `empfang` (gymId), `empfangAnbieten` (gymId, kundeId, art, was), `empfangKaufen` (gymId, art, was), `angebotAntwort` (ja), `chefAktion` (preise, daten)
- Coaching: `coachen` (kundeId), `trainerListe`, `trainingAbschliessen` (buchungId, betrag; 0 = verwerfen)
- Kurse: `kurse`, `kursAnlegen`, `kursAbsagen` (id), `kursAnmelden` (id, dabei), `kursAufbauen` (id, aufbauen), `kursMatten` (id, Liste x/y/z/h/hoehe), `kursStarten` (id)
- Admin (ACE command.fitnessplace): `adminPruefen`, `adminBefehl` (befehl, argumente), `gymAktion` (aktion, daten), Aktionen u. a. neu, umbenennen, loeschen, station, karte, spieler, werte, freigeben, kater, wirkung, probetag, einstellungen, empfangSetzen, empfangLoeschen, platzieren, kurseAlle, kursAbsagen, besten, bestenLoeschen, bestenReset

Client-Events (`toter-fitness:client:…`): `admin`, `adminDaten`, `angebot`, `ausgabe`, `bestenliste`, `coachen`, `einnehmen`, `empfang`, `gyms`, `karteZeigen`, `kurse`, `routeZeigen`, `spieler`, `start`, `stationen`, `stats`, `trainerListe`, `verlauf`, dazu lokal `zielStatus` (an Client/ziel.lua).

NUI-Callbacks Admin-Menü und Minispiele: `menueBefehl`, `gymAktion`, `platzieren`, `propVorschau`, `diagnose`, `teleport`, `wegpunkt`, `empfangAnbieten`, `empfangKaufen`, `angebotAntwort`, `close`, `phase`, `ergebnis`, `treffer`. Die Apps nutzen eigene Callbacks (siehe Handy-Apps).

Exports: Server `GetStats(citizenid)`, `Koerper(source)`, `KannTragen(traeger, ziel)`; Client `Koerper()`, `KannTragen(zielServerId)`.

## Datenbank
- `player_fitness`: Werte pro citizenid, last_training, last_abbau, total_training_sessions, last_<wert>, abbau_<wert>
- `fitness_stations`: Stationen mit Position, Ausrichtung, prop_model, prop_height, route ("x,y,z;x,y,z"), gym_id
- `fitness_rekorde`: citizenid, uebung, wert, name, datum
- `fitness_gyms`: id, name, created_at, job, preis_tag, preis_woche, preis_monat, empfang ("x,y,z"), shop ("item=preis;…")
- `sql/tabellen.sql` zeigt alle Tabellen in ihrer aktuellen Form (aus dem Server-Code erzeugt). Anlegen und Ergänzen macht der Server selbst.

## Anschlüsse
Fremde Ressourcen (Framework, Geld, Inventar, Rechnung, Steuer, Meldungen, Aussehen, Handy, Ziel-Menü) nur über Server/Bridge.cs, Client/Bridge.cs, Client/Telefon.cs (AppSetzen, AppSenden) und Client/ziel.lua. Allgemeine Beschreibung für jeden Server mit Beispielen für Qbox, ESX, ox_inventory, ox_lib: ANSCHLUESSE.md.

## Entscheidungen
- Lebenspunkte und Regeneration fasst die Mod nicht an (der Server hat Regeneration abgeschaltet). Kein SetPlayerHealthRechargeMultiplier, kein SetEntityHealth außer der abschaltbaren Sturz-Option.
- GetGameTimer() ist auf dem Server long, im Client int. Auf dem Server immer long verwenden.
- Im Client kein `yield return` (Iteratoren): der C#-Compiler nutzt dafür Environment.CurrentManagedThreadId, das FiveM sperrt. Listen zurückgeben.
- Props erzeugt jeder Client selbst (nicht vernetzt), dadurch keine doppelten Objekte und sie sind nach dem Verbinden sofort da.
- Marker werden jeden Frame gezeichnet (eigener Tick), die Suche nach nahen Stationen läuft getrennt alle 500 ms.
- Kein Newtonsoft.Json: NUI-Nachrichten schreibt `Shared/Json.cs`.
- Kein System.Text.RegularExpressions im Client: wirft in FiveM MethodAccessException.
- Keine Chat-Befehle außer /fitnessadmin. Spieler nur über die Handy-Apps, kein Menü im Spiel.
- Kein Lua außer Client/ziel.lua (ox_target und qb-target rufen canInteract/onSelect direkt auf, C#-Callbacks gehen dort nicht). Sonst alles in C#. FiveM-eigene Natives ohne C#-Wrapper über Function.Call mit dem Hash (Jenkins-Hash des Namens).
- Wenig Kommentare im Code, Erklärungen stehen hier, in Config.cs und in den Bridge-Dateien.
- Der Server prüft Abstand, einmal pro Tag je Wert, Abklingzeit (Config, derzeit 0), Bezahlung bzw. Mitgliedskarte und ein Token pro Training. Punkte vom Client werden auf 0–100 begrenzt, unter 8 Sekunden Trainingsdauer zählen sie nicht.
- Abbau pro Wert: 2 Tage nach dem letzten Training dieses Werts, danach pro vollem Tag −1 ohne gültige Gym-Karte, −2 mit Gym-Karte (Config.AbbauOhneMitgliedschaft/AbbauMitMitgliedschaft), nie unter 20. Spalten last_<wert> und abbau_<wert> in player_fitness (legt der Server selbst an). Verrechnet beim Laden und jede Minute für Spieler online.
- Effekte (Client/Effekte.cs): jeder Wert hat eigene Effekte, keiner doppelt. Start ist 50, zwischen 35 und 65 neutral (Config.NegativUnter/PositivAb), darunter Nachteile, ab 65 Vorteile, volle Wirkung ab 90 (Config.VollAb).
  - Kraft: Nahkampfschaden 1,0× im neutralen Bereich, bis 1,2× ab 90, bis 0,8× bei 0. GTA-Stärke (MP0_STRENGTH). Unter 25 schlaffe Gangart. Kräftige Gangart ist aus (Config.GangStark leer), weil sie Laufen und Rennen langsamer macht.
  - Ausdauer: Tempo. Ab 65 schneller sprinten (bis +8 %, Config.SprintTempoPlus) und schwimmen (bis +15 %, SchwimmTempoPlus). Langsamer machen erlaubt GTA nicht, Nachteil über den Körpertyp.
  - Kondition: Puste. Am Stück sprinten 8 s bei 0, 30 s bei 65 (SprintNormalSekunden), 60 s kurz vor 90, ab 90 Dauersprint. Danach Luft holen 6 s bei 0, 4 s im neutralen Bereich, 2 s ab 90: Sprint und Sprung gesperrt, Animation re@construction/out_of_breath nur Oberkörper. GTA-Ausdauerbalken (MP0_STAMINA) folgt der Kondition. Keine Selbstheilung, kein Tauchen.
  - Flexibilität: weniger Sturzschaden (Config.FallschadenWeniger, Standard 0 = aus, weil es technisch Lebenspunkte zurückgibt). Unter 35 gelegentliches Stolpern beim Sprinten, je weniger, desto öfter (höchstens alle 20 s).
  - Ausdauer und Kondition beide unter 25: schwerfällige ("fette") Gangart, volleres Gesicht und dicker Hals. Kraft unter 25: eingefallenes Gesicht.
  - Gesichtsänderungen nur auf Freemode-Charakteren, nicht gespeichert, alle 10 s erneuert. Werden die Werte normal, lädt qb-clothing den gespeicherten Charakter neu (Config.SkinNeuLaden...).
  - Gangart wird nur gesetzt, wenn sie sich ändert. Abschaltbar mit Config.GangartAnpassen.
- Körpertyp (Config.KoerperTyp): "fett" (Ausdauer und Kondition unter 25), "athletisch" (Kraft ab 70 und Schnitt ab 60), sonst "normal". Der Server setzt ihn als Statebag `toterKoerper`.
- Körper-Kleidung: Ressource `toter-koerper` (Kollix Fat Pack + mp25 Muscular Body, nur MP Male). Das Kollix Fat Pack ist in FiveM kaputt (Hoodie und Hose unsichtbar), deshalb ist Config.FettKleidung aus und "fett" sieht man nur an Gesicht und Gang, bei "athletisch" ohne Oberteil einen muskulösen Oberkörper (danach der vorige Oberkörper). Die Nummern sucht Client/Bridge.cs (KleidungNummer, FiveM-Natives für Kleidungs-Sammlungen per Hash) über den Paketnamen (Config.FettPaket "kollixfat", Config.AthletischPaket "muscularbody"), weil andere Kleidungspakete die Nummern verschieben. Nur wenn das nicht klappt, gelten die Nummern in Config.cs (negativ = vom Ende). Ungültige Kleidung wird nie angezogen (sonst unsichtbarer Körper), stattdessen wird das gespeicherte Outfit geladen.
- Tragen: Exports für Trage-Skripte. Server `exports['toter-fitness']:KannTragen(traegerSource, zielSource)`, Client `exports['toter-fitness']:KannTragen(zielServerId)`, dazu `Koerper(...)`. Regel: "fett" kann nur tragen, wer "athletisch" ist.
- Studio-Minispiele sind spürbar leichter als die freien (dafür kosten sie Geld und geben mehr). Beim Kraft-Spiel sinkt der Balken bis zum ersten Erreichen von 80 % nur halb so schnell.
- Eine Station kann nur eine Person gleichzeitig nutzen, außer Joggen (gemeinsam erlaubt). Hängende Sitzungen verfallen nach 10 Minuten.
- Wer trainiert, bekommt den Statebag `toterFitness = true`. Andere Clients sperren in 6 m Umkreis Angriffstasten und Kollision mit dieser Person. Wird die Übung trotzdem unterbrochen, nimmt der Client sie nach spätestens 0,5 s wieder auf.
- Hanteln, die das Bankdrücken-Szenario erzeugt, werden nach dem Training erst aus der Hand gelöst und dann gelöscht (Hanteln, die vorher schon da lagen, bleiben). Szenario-Hanteln gibt es auf jedem Client einzeln: Jeder Client merkt sich, welche anderen Spieler gerade trainieren, und räumt deren Hanteln bei sich weg, sobald sie fertig sind (Schutz.cs, andereTraining). Nach jeder Übung setzt SteuerungFreigeben() Waffe auf unbewaffnet, Waffenwechsel, Steuerung, Ragdoll und Einfrieren zurück, sonst ging nach dem Bankdrücken kein Schlagen mehr.
- Während des Trainings ist der Charakter unverwundbar, nicht schlagbar (kein Schaden, kein Ragdoll durch andere) und sichtbar. Unsichtbare Spieler (Noclip) können nicht trainieren. Wer die Station verlässt, bricht ab; der Server wertet nur, wenn der Spieler noch an der Station bzw. an der Strecke ist.
- Joggen: Der erste Streckenpunkt ist immer die Station. Das Dino-Spiel läuft genau eine Runde lang und wird mit dem Fortschritt der Runde schneller (Client meldet `fortschritt`/`rundeFertig` an die NUI). Der Charakter wechselt 3,5 m vor jedem Punkt zum nächsten (Config.RoutePunktRadius), damit er nicht abbremst. Jeder Treffer lässt den Charakter kurz stolpern (Laufband: beim 1. und 2. Treffer nur außer Atem mit re@construction/out_of_breath, Uebung.TrefferDict/Clip; erst das letzte Leben wirft einen herunter) (zufällig stark, beim letzten Leben heftiger). Alle Leben weg: Training sofort vorbei mit Patzer, auch mitten in der Runde.
- Unter 35 % geht die Übung schief: Ragdoll mit Stoß je Übung (z. B. Laufband schleudert nach hinten) und ein Spruch. Werte in Config.cs (PatzerSeite/Vor/Hoch, PatzerText).
- Platz (Client/Platz.cs): Bei Übungen mit Prop steht der Charakter auf der Mitte der Prop-Box, ausgerichtet an der langen Seite ("laengs": Matten, Bank, Laufband) oder davor ("quer": Klimmzugstange). Feinjustierung PedVor/PedSeite/PedHoch/Blickrichtung pro Übung in Config.cs. Der Charakter wird während der Übung festgehalten (FreezeEntityPosition), sonst schwebt er bei Szenarien wie Bankdrücken über dem Prop.
- Szenarien starten "in place" am berechneten Platz. Liegestütze, Sit-ups und Yoga laufen als Animation, nur die Klimmzüge sind an das Kraft-Spiel gekoppelt (Steuerbar). Unter der Schwelle spielt die Animation die laufende Wiederholung zu Ende und hält an der RuhePhase an (unten hängend), statt mitten in der Bewegung einzufrieren.
- Blickrichtung pro Übung: Laufband 180°, weil das Modell andersherum ausgerichtet ist.

## Offen
- Prop-Modelle auf neuen Servern prüfen (Diagnose im Admin-Menü zeigt, ob ein Modell fehlt). Ersatz im Admin-Menü unter Prop, z. B. `prop_muscle_bench_03`. Klimmzüge nutzen `prop_beach_bars_02`, Versatz: PedVor -0.16, PedHoch 0.12, RuhePhase 0.429.
- Gym-Job mit den Rängen 0 Rezeption, 1 Trainer, 2 Chef anlegen (sonst Config.RangTrainer/RangChef anpassen).
