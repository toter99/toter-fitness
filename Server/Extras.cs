using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Server
{
    public partial class Main
    {
        private async Task ExtrasVorbereiten()
        {
            await Sql("update", "CREATE TABLE IF NOT EXISTS `fitness_rekorde` (`citizenid` VARCHAR(50) NOT NULL, `uebung` VARCHAR(30) NOT NULL, " +
                "`wert` INT NOT NULL DEFAULT 0, `name` VARCHAR(60) NOT NULL DEFAULT '', `datum` INT UNSIGNED NOT NULL DEFAULT 0, " +
                "PRIMARY KEY (`citizenid`, `uebung`), KEY `uebung_wert` (`uebung`, `wert`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            foreach (var spalte in new[] { "buff_shake_bis", "buff_vitamine_bis", "muskelkater_ab", "muskelkater_bis" })
                await Sql("update", $"ALTER TABLE `player_fitness` ADD COLUMN IF NOT EXISTS `{spalte}` INT UNSIGNED NOT NULL DEFAULT 0");
            SupplementsRegistrieren();
        }

        private async Task RekordPruefen(Player player, string cid, string uebung, object rekordRoh)
        {
            int wert;
            try { wert = Convert.ToInt32(rekordRoh); }
            catch { return; }
            var u = Config.Uebungen.ContainsKey(uebung) ? Config.Uebungen[uebung] : null;
            if (u == null || wert <= 0) return;
            wert = Math.Min(wert, Config.RekordMaximum(u.Spiel, u.Dauer));

            var alt = await Abfrage("SELECT wert FROM fitness_rekorde WHERE citizenid = ? AND uebung = ?", cid, uebung);
            var bisher = alt.Count > 0 ? Zahl(alt[0], "wert") : 0;
            if (wert <= bisher) return;
            await Sql("update",
                "INSERT INTO fitness_rekorde (citizenid, uebung, wert, name, datum) VALUES (?, ?, ?, ?, ?) " +
                "ON DUPLICATE KEY UPDATE wert = VALUES(wert), name = VALUES(name), datum = VALUES(datum)",
                cid, uebung, wert, Charname(player), Jetzt());
            Melden(player, bisher > 0
                ? $"Neuer persönlicher Rekord bei {u.Name}: {wert} {Config.RekordEinheit(u.Spiel)} (vorher {bisher})!"
                : $"Erster Rekord bei {u.Name}: {wert} {Config.RekordEinheit(u.Spiel)}.", "success");
        }

        [EventHandler("toter-fitness:server:bestenliste")]
        private async void OnBestenliste([FromSource] Player player, string uebung)
        {
            if (Gedrosselt(player, "OnBestenliste", 1000)) return;
            try
            {
                if (!Config.Uebungen.ContainsKey(uebung)) return;
                var u = Config.Uebungen[uebung];
                var zeilen = await Abfrage("SELECT citizenid, name, wert, datum FROM fitness_rekorde WHERE uebung = ? ORDER BY wert DESC, datum ASC LIMIT 10", uebung);
                var cid = CitizenId(player);
                var eigen = await Abfrage("SELECT wert FROM fitness_rekorde WHERE citizenid = ? AND uebung = ?", cid ?? "", uebung);
                TriggerClientEvent(player, "toter-fitness:client:bestenliste", new Dictionary<string, object>
                {
                    { "uebung", uebung },
                    { "einheit", Config.RekordEinheit(u.Spiel) },
                    { "eigen", eigen.Count > 0 ? Zahl(eigen[0], "wert") : 0 },
                    { "liste", zeilen.Select(z => (object)new Dictionary<string, object>
                        {
                            { "name", Text(z, "name") }, { "wert", Zahl(z, "wert") }, { "datum", Datum(GrosseZahl(z, "datum")).Substring(0, 10) },
                            { "ich", Text(z, "citizenid") == cid }
                        }).ToList() }
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Bestenliste: {e.Message}");
            }
        }

        private void SupplementsRegistrieren()
        {
            foreach (var s in Config.Supplements)
            {
                var item = s;
                ItemAnlegen(item.Item, item.Name, 200, false, item.Beschreibung);
                ItemBenutzbar(item.Item, (quelle, roh) => SupplementBenutzt(quelle, item, roh));
            }
        }

        private async void SupplementBenutzt(int quelle, Supplement s, object itemRoh)
        {
            try
            {
                var player = SpielerVon(quelle);
                var cid = player != null ? CitizenId(player) : null;
                if (cid == null) return;
                var w = await WerteHolen(cid, player);
                var jetzt = Jetzt();

                if (s.Wirkung == "muskelkater" && !(w.MuskelkaterBis > jetzt))
                {
                    Melden(player, "Du hast gerade keinen Muskelkater.", "primary");
                    return;
                }

                var item = AlsTabelle(itemRoh);
                object slot = item != null && item.ContainsKey("slot") ? item["slot"] : null;
                if (!ItemNehmen(player, s.Item, 1, slot)) return;

                switch (s.Wirkung)
                {
                    case "zugewinn":
                        w.ShakeBis = Math.Max(w.ShakeBis, jetzt) + s.Sekunden;
                        Melden(player, $"{s.Name}: +{Config.ShakePunkte} pro Übung bis {Datum(w.ShakeBis).Substring(11)}.", "success");
                        break;
                    case "abbau":
                        Abbauen(w, AbbauRate(player, cid));
                        w.VitamineBis = Math.Max(w.VitamineBis, jetzt) + s.Sekunden;
                        Melden(player, $"{s.Name}: kein Abbau deiner Werte bis {Datum(w.VitamineBis)}.", "success");
                        break;
                    case "muskelkater":
                        w.MuskelkaterBis = 0;
                        w.MuskelkaterAb = 0;
                        Melden(player, $"{s.Name}: der Muskelkater ist weg.", "success");
                        break;
                }
                await Speichern(cid, w);
                StatsSenden(player, w, false);
                TriggerClientEvent(player, "toter-fitness:client:einnehmen", s.Animation);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] {s.Item}: {e}");
            }
        }

        private class Buchung
        {
            public int Id;
            public string Trainer, TrainerName, Kunde, KundeName, Job;
            public int Einheiten, KursId;
            public string KursTitel;
            public long Bis, Ende;
            public readonly Dictionary<string, int> Bonus = new Dictionary<string, int>();  // Trainer-Bonus, gilt erst nach Bezahlung
        }

        // laufende Coachings (Kunde -> Buchung) und beendete, die der Trainer noch abrechnen oder verwerfen kann
        private readonly Dictionary<string, Buchung> buchungen = new Dictionary<string, Buchung>();
        private readonly List<Buchung> offeneBuchungen = new List<Buchung>();
        private int buchungNummer;

        private Gym GymVonMitarbeiter(Player player)
        {
            return gyms.FirstOrDefault(g => IstMitarbeiter(player, g));
        }

        private bool IstTrainer(Player player)
        {
            if (GymVonMitarbeiter(player) == null) return false;
            var job = JobVon(player);
            return job.Rang >= Config.RangTrainer || job.Chef;
        }

        private object TrainerDaten(Player trainer)
        {
            var kunden = new List<object>();
            foreach (var b in buchungen.Values.Where(x => x.Trainer == trainer.Handle).Concat(offeneBuchungen.Where(x => x.Trainer == trainer.Handle)))
            {
                var laeuft = buchungen.ContainsKey(b.Kunde) && buchungen[b.Kunde] == b;
                Sitzung s;
                var u = laeuft && sitzungen.TryGetValue(b.Kunde, out s) ? Config.UebungFuer(s.Stat, s.Art) : null;
                kunden.Add(new Dictionary<string, object>
                {
                    { "id", b.Id }, { "name", b.KundeName }, { "status", laeuft ? "laeuft" : "offen" }, { "kurs", b.KursTitel ?? "" },
                    { "einheiten", b.Einheiten }, { "bis", laeuft && b.Bis > Jetzt() + 86400L * 365 ? "" : Datum(laeuft ? b.Bis : b.Ende).Substring(11) },
                    { "uebung", u != null ? u.Name : "" }
                });
            }
            return new Dictionary<string, object>
            {
                { "kunden", kunden }, { "preis", Config.TrainerStandardPreis }, { "max", Config.TrainerMaxPreis },
                { "steuer", (int)Math.Round(Steuersatz() * 100) }
            };
        }

        private void TrainerListeSenden(Player trainer)
        {
            if (trainer != null) TriggerClientEvent(trainer, "toter-fitness:client:trainerListe", TrainerDaten(trainer));
        }

        [EventHandler("toter-fitness:server:trainerListe")]
        private void OnTrainerListe([FromSource] Player player)
        {
            if (Gedrosselt(player, "OnTrainerListe", 500)) return;
            if (IstTrainer(player)) TrainerListeSenden(player);
        }

        // Trainer wählt im Alt-Menü (Ziel-Menü) einen Spieler: Der Spieler wird gefragt, ob er gecoacht werden will.
        [EventHandler("toter-fitness:server:coachen")]
        private void OnCoachen([FromSource] Player trainer, int kundeId)
        {
            if (Gedrosselt(trainer, "OnCoachen", 2000)) return;
            var g = GymVonMitarbeiter(trainer);
            var kunde = SpielerVon(kundeId);
            if (!IstTrainer(trainer) || g == null)
            {
                Melden(trainer, "Coachen können nur Trainer im Dienst.", "error");
                return;
            }
            if (kunde == null || kunde.Handle == trainer.Handle) return;
            if (!Nah(trainer, kunde, Config.TrainerAbstand))
            {
                Melden(trainer, "Du stehst zu weit weg.", "error");
                return;
            }
            Buchung b;
            if (buchungen.TryGetValue(kunde.Handle, out b))
            {
                Melden(trainer, b.Trainer == trainer.Handle ? $"Du coachst {Charname(kunde)} schon." : $"{Charname(kunde)} wird schon von {b.TrainerName} gecoacht.", "error");
                return;
            }
            var a = new Angebot { Art = "coaching", GymId = g.Id, Verkaeufer = trainer.Handle, Bis = Jetzt() + Config.AngebotSekunden };
            AngebotSchicken(trainer, kunde, a, "Personal Training", $"+{Config.TrainerPunkte} pro Übung mit Trainer");
        }

        private void CoachingStarten(Player kunde, Player trainer, Angebot a)
        {
            if (trainer == null)
            {
                Melden(kunde, "Der Trainer ist nicht mehr da.", "error");
                return;
            }
            var g = gyms.FirstOrDefault(x => x.Id == a.GymId);
            buchungen[kunde.Handle] = new Buchung
            {
                Id = ++buchungNummer, Trainer = trainer.Handle, TrainerName = Charname(trainer), Kunde = kunde.Handle, KundeName = Charname(kunde),
                Job = g?.Job ?? "", Bis = Config.TrainerBuchungMinuten > 0 ? Jetzt() + Config.TrainerBuchungMinuten * 60L : long.MaxValue / 4
            };
            Melden(kunde, $"{Charname(trainer)} coacht dich jetzt.", "success");
            Melden(trainer, $"Du coachst {Charname(kunde)}. Bleib bei den Übungen in der Nähe, abrechnen kannst du im GymDesk.", "success");
            Sitzung s;
            if (sitzungen.TryGetValue(kunde.Handle, out s)) CoachVerbinden(kunde, s);
            TrainerListeSenden(trainer);
        }

        private void CoachVerbinden(Player kunde, Sitzung s)
        {
            Buchung b;
            if (!buchungen.TryGetValue(kunde.Handle, out b) || b.Bis < Jetzt()) return;
            var trainer = SpielerVon(int.Parse(b.Trainer));
            var reichweite = b.KursId > 0 ? Config.KursRadius : Config.TrainerAbstand * 2;
            if (trainer == null || !Nah(trainer, kunde, reichweite)) return;
            s.Coach = trainer.Handle;
            s.CoachName = b.KursId > 0 ? $"Kurs „{b.KursTitel}“" : b.TrainerName;
            var u = Config.UebungFuer(s.Stat, s.Art);
            if (b.KursId > 0 || sitzungen.ContainsKey(trainer.Handle)) return;
            Melden(trainer, $"{Charname(kunde)} startet: {u?.Name}.", "primary");
            if (u != null && !u.Route) TriggerClientEvent(trainer, "toter-fitness:client:coachen", int.Parse(kunde.Handle), CoachAmBoden(u), true);
        }

        // Trainer-Bonus nach Bezahlung (oder kostenlosem Coaching) gutschreiben.
        private async Task BonusGutschreiben(Player kunde, Buchung b)
        {
            if (kunde == null || b.Bonus.Count == 0) return;
            var cid = CitizenId(kunde);
            if (cid == null) return;
            var w = await WerteHolen(cid, kunde);
            var teile = new List<string>();
            foreach (var kv in b.Bonus)
            {
                var alt = w.Stat[kv.Key];
                w.Stat[kv.Key] = Begrenzen(alt + kv.Value);
                if (w.Stat[kv.Key] > alt) teile.Add($"{Config.StatNamen[kv.Key]} +{w.Stat[kv.Key] - alt}");
            }
            b.Bonus.Clear();
            await Speichern(cid, w);
            if (teile.Count > 0) Melden(kunde, $"{(b.KursId > 0 ? "Kurs-Bonus" : "Trainer-Bonus")} gutgeschrieben: {string.Join(", ", teile)}.", "success");
            StatsSenden(kunde, w, false);
        }

        private static bool CoachAmBoden(Uebung u)
        {
            return u.Sitzend || u.Stat == "kondition" || u.Stat == "flexibilitaet";
        }

        private void CoachBeenden(Sitzung s)
        {
            if (s?.Coach == null) return;
            var trainer = SpielerVon(int.Parse(s.Coach));
            if (trainer != null) TriggerClientEvent(trainer, "toter-fitness:client:coachen", 0, false, false);
        }

        private void CoachingEnde(Buchung b, bool melden)
        {
            if (buchungen.ContainsKey(b.Kunde) && buchungen[b.Kunde] == b) buchungen.Remove(b.Kunde);
            if (!offeneBuchungen.Contains(b)) offeneBuchungen.Add(b);
            b.Ende = Jetzt();
            if (!melden) return;
            var kunde = SpielerVon(int.Parse(b.Kunde));
            if (kunde != null) Melden(kunde, $"Das Coaching mit {b.TrainerName} ist vorbei.", "primary");
            var trainer = SpielerVon(int.Parse(b.Trainer));
            if (trainer != null)
            {
                Melden(trainer, $"Coaching mit {b.KundeName} ist vorbei. Abrechnen oder verwerfen im GymDesk.", "primary");
                TrainerListeSenden(trainer);
            }
        }

        // Trainer rechnet ab (betrag > 0) oder verwirft (kostenlos). Laufende Coachings werden dabei beendet.
        [EventHandler("toter-fitness:server:trainingAbschliessen")]
        private async void OnTrainingAbschliessen([FromSource] Player trainer, int id, int betrag)
        {
            if (Gedrosselt(trainer, "OnTrainingAbschliessen", 200)) return;
            var b = buchungen.Values.Concat(offeneBuchungen).FirstOrDefault(x => x.Id == id && x.Trainer == trainer.Handle);
            if (b == null) return;
            await Abschliessen(trainer, b, betrag);
        }

        private async Task Abschliessen(Player trainer, Buchung b, int betrag)
        {
            CoachingEnde(b, false);
            offeneBuchungen.Remove(b);
            var kunde = SpielerVon(int.Parse(b.Kunde));
            betrag = Math.Max(0, Math.Min(Config.TrainerMaxPreis, betrag));

            if (betrag == 0)
            {
                if (kunde != null) Melden(kunde, b.KursId > 0 ? $"Der Kurs „{b.KursTitel}“ ist kostenlos." : $"Das Coaching mit {b.TrainerName} war kostenlos.", "success");
                await BonusGutschreiben(kunde, b);
                if (b.KursId == 0) Melden(trainer, $"Coaching mit {b.KundeName} verworfen, keine Rechnung.", "primary");
                TrainerListeSenden(trainer);
                return;
            }
            if (kunde == null)
            {
                Melden(trainer, $"{b.KundeName} ist nicht mehr da, die Rechnung geht nicht raus.", "error");
                TrainerListeSenden(trainer);
                return;
            }
            var grund = b.KursId > 0
                ? $"Kurs „{b.KursTitel}“ bei {b.TrainerName}"
                : $"Personal Training ({b.Einheiten} {(b.Einheiten == 1 ? "Übung" : "Übungen")}) bei {b.TrainerName}";
            if (b.Bonus.Count > 0) grund += $", {(b.KursId > 0 ? "Bonus" : "Trainer-Bonus")} {string.Join(", ", b.Bonus.Select(kv => $"{Config.StatNamen[kv.Key]} +{kv.Value}"))} nach Bezahlung";
            if (Config.RechnungMitZustimmung)
            {
                var g = gyms.FirstOrDefault(x => x.Job == b.Job);
                var a = new Angebot { Art = "rechnung", GymId = g?.Id ?? 0, Preis = betrag, Verkaeufer = trainer.Handle, Item = grund, Tarif = b.Job, Bis = Jetzt() + Config.AngebotSekunden, Buchung = b };
                AngebotSchicken(trainer, kunde, a, "Rechnung", grund);
            }
            else RechnungBezahlt(kunde, trainer, betrag, grund, b.Job, b);
            TrainerListeSenden(trainer);
        }

        private async void RechnungBezahlt(Player kunde, Player trainer, int betrag, string grund, string job, Buchung b)
        {
            if (RechnungStellen(trainer, kunde, betrag, grund, job))
            {
                Melden(kunde, $"Rechnung über ${betrag} bezahlt.", "success");
                if (b != null) await BonusGutschreiben(kunde, b);
                if (trainer != null) Melden(trainer, $"{Charname(kunde)} hat ${betrag} bezahlt, das geht nach Steuern ans Gym.", "success");
            }
            else
            {
                Melden(kunde, $"Die Rechnung über ${betrag} konnte nicht bezahlt werden.", "error");
                if (trainer != null) Melden(trainer, $"{Charname(kunde)} konnte die ${betrag} nicht bezahlen.", "error");
            }
        }

        private void BuchungenPruefen()
        {
            foreach (var b in buchungen.Values.ToList())
                if (b.Bis <= Jetzt() || SpielerVon(int.Parse(b.Trainer)) == null || SpielerVon(int.Parse(b.Kunde)) == null) CoachingEnde(b, true);
            var alt = Jetzt() - Config.AbrechnenStunden * 3600L;
            foreach (var b in offeneBuchungen.ToList())
                if (b.Ende < alt || SpielerVon(int.Parse(b.Trainer)) == null)
                {
                    offeneBuchungen.Remove(b);
                    Debug.WriteLine($"[{Config.Ressource}] Coaching {b.TrainerName} mit {b.KundeName} ohne Abrechnung verfallen.");
                }
        }

        private void MuskelkaterPruefen(Player player, Werte w)
        {
            var tag = TagesStart();
            if (!Config.Stats.All(st => w.LetztesStat.ContainsKey(st) && w.LetztesStat[st] >= tag)) return;
            var morgen = tag + 86400;
            if (w.MuskelkaterAb == morgen) return;
            var schnitt = Config.Stats.Average(st => w.Stat[st]);
            if (schnitt >= Config.MuskelkaterSchutzAb)
            {
                Melden(player, "Viel trainiert heute, aber das steckst du locker weg.", "primary");
                return;
            }
            w.MuskelkaterAb = morgen;
            w.MuskelkaterBis = morgen + Config.MuskelkaterStunden * 3600L;
            Melden(player, $"Das war heute ganz schön viel. Morgen früh hast du Muskelkater, bis etwa {Datum(w.MuskelkaterBis).Substring(11)} Uhr.", "primary");
        }

        private static bool HatMuskelkater(Werte w)
        {
            var jetzt = Jetzt();
            return w.MuskelkaterAb > 0 && jetzt >= w.MuskelkaterAb && jetzt < w.MuskelkaterBis;
        }
    }
}
