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
        private async Task<Werte> WerteHolen(string cid, Player player = null)
        {
            Werte w;
            if (werte.TryGetValue(cid, out w)) return w;

            await Sql("update", "INSERT IGNORE INTO player_fitness (citizenid) VALUES (?)", cid);
            var zeilen = await Abfrage("SELECT * FROM player_fitness WHERE citizenid = ?", cid);
            var z = zeilen.FirstOrDefault();
            w = new Werte
            {
                LetztesTraining = GrosseZahl(z, "last_training"),
                LetzterAbbau = GrosseZahl(z, "last_abbau"),
                Einheiten = Zahl(z, "total_training_sessions")
            };
            foreach (var stat in Config.Stats)
            {
                w.Stat[stat] = Begrenzen(Zahl(z, stat, Config.StatStart));
                var letztes = GrosseZahl(z, "last_" + stat);
                w.LetztesStat[stat] = letztes > 0 ? letztes : w.LetztesTraining;
                var bis = GrosseZahl(z, "abbau_" + stat);
                w.AbbauBis[stat] = bis > 0 ? bis : w.LetzterAbbau;
            }
            w.ShakeBis = GrosseZahl(z, "buff_shake_bis");
            w.VitamineBis = GrosseZahl(z, "buff_vitamine_bis");
            w.MuskelkaterAb = GrosseZahl(z, "muskelkater_ab");
            w.MuskelkaterBis = GrosseZahl(z, "muskelkater_bis");

            Abbauen(w, AbbauRate(player, cid));
            werte[cid] = w;
            await Speichern(cid, w);
            return w;
        }

        private static bool Abbauen(Werte w, int rate)
        {
            var jetzt = Jetzt();
            var geaendert = false;
            var geschuetztBis = Math.Min(jetzt, w.VitamineBis);
            foreach (var stat in Config.Stats)
            {
                var letztes = w.LetztesStat.ContainsKey(stat) ? w.LetztesStat[stat] : 0;
                var bis = w.AbbauBis.ContainsKey(stat) ? w.AbbauBis[stat] : 0;
                if (bis > 0 && geschuetztBis > bis) { w.AbbauBis[stat] = bis = geschuetztBis; geaendert = true; }
                if (letztes == 0 && bis == 0)
                {
                    w.AbbauBis[stat] = jetzt;
                    continue;
                }
                var ab = Math.Max(letztes + Config.SchonfristTage * 86400L, bis);
                if (jetzt <= ab) continue;
                var tage = (jetzt - ab) / 86400;
                if (tage <= 0) continue;
                var alt = w.Stat[stat];
                if (alt > Config.AbbauUntergrenze)
                    w.Stat[stat] = Math.Max(Config.AbbauUntergrenze, alt - (int)tage * rate);
                w.AbbauBis[stat] = ab + tage * 86400;
                geaendert = true;
            }
            if (geaendert) w.LetzterAbbau = jetzt;
            return geaendert;
        }

        private static int Begrenzen(int wert)
        {
            return Math.Max(Config.StatMin, Math.Min(Config.StatMax, wert));
        }

        private async Task Speichern(string cid, Werte w)
        {
            Func<Dictionary<string, long>, string, long> holen = (d, k) => d.ContainsKey(k) ? d[k] : 0;
            await Sql("update",
                "UPDATE player_fitness SET kraft = ?, ausdauer = ?, flexibilitaet = ?, kondition = ?, last_training = ?, last_login = ?, last_abbau = ?, total_training_sessions = ?, " +
                "last_kraft = ?, last_ausdauer = ?, last_flexibilitaet = ?, last_kondition = ?, abbau_kraft = ?, abbau_ausdauer = ?, abbau_flexibilitaet = ?, abbau_kondition = ?, " +
                "buff_shake_bis = ?, buff_vitamine_bis = ?, muskelkater_ab = ?, muskelkater_bis = ? WHERE citizenid = ?",
                w.Stat["kraft"], w.Stat["ausdauer"], w.Stat["flexibilitaet"], w.Stat["kondition"],
                w.LetztesTraining, Jetzt(), w.LetzterAbbau, w.Einheiten,
                holen(w.LetztesStat, "kraft"), holen(w.LetztesStat, "ausdauer"), holen(w.LetztesStat, "flexibilitaet"), holen(w.LetztesStat, "kondition"),
                holen(w.AbbauBis, "kraft"), holen(w.AbbauBis, "ausdauer"), holen(w.AbbauBis, "flexibilitaet"), holen(w.AbbauBis, "kondition"),
                w.ShakeBis, w.VitamineBis, w.MuskelkaterAb, w.MuskelkaterBis,
                cid);
            await Sql("update", "INSERT INTO fitness_verlauf (citizenid, tag, kraft, ausdauer, flexibilitaet, kondition) VALUES (?, ?, ?, ?, ?, ?) " +
                "ON DUPLICATE KEY UPDATE kraft = VALUES(kraft), ausdauer = VALUES(ausdauer), flexibilitaet = VALUES(flexibilitaet), kondition = VALUES(kondition)",
                cid, TagesStart(), w.Stat["kraft"], w.Stat["ausdauer"], w.Stat["flexibilitaet"], w.Stat["kondition"]);
        }

        private void StatsSenden(Player player, Werte w, bool oeffnen, bool voll = false)
        {
            var stats = new Dictionary<string, object>();
            foreach (var stat in Config.Stats) stats[stat] = w.Stat[stat];
            stats["einheiten"] = w.Einheiten;
            var tag = TagesStart();
            stats["heute"] = Config.Stats.Where(st => w.LetztesStat.ContainsKey(st) && w.LetztesStat[st] >= tag && !w.Freigegeben.Contains(st))
                .Cast<object>().ToList();
            stats["reset"] = Config.TagesResetStunde;
            var jetzt = Jetzt();
            stats["muskelkater"] = HatMuskelkater(w) ? Datum(w.MuskelkaterBis) : "";
            stats["muskelkaterMorgen"] = w.MuskelkaterAb > jetzt ? Datum(w.MuskelkaterAb) : "";
            stats["shake"] = w.ShakeBis > jetzt ? Datum(w.ShakeBis) : "";
            stats["vitamine"] = w.VitamineBis > jetzt ? Datum(w.VitamineBis) : "";
            stats["studio"] = StudioVon(player);
            if (oeffnen || voll)
            {
                stats["chef"] = ChefDaten(player);
                stats["kurse"] = KurseDaten(player);
                stats["trainer"] = IstTrainer(player);
                if (IstTrainer(player)) stats["trainerDaten"] = TrainerDaten(player);
                var cid = CitizenId(player);
                stats["karten"] = KartenVon(player).Where(k => k.CitizenId == cid && k.Ablauf > Jetzt())
                    .Select(k => (object)new Dictionary<string, object> { { "gym", k.Gym }, { "bis", Datum(k.Ablauf) } }).ToList();
            }
            TriggerClientEvent(player, "toter-fitness:client:stats", stats, oeffnen);
            try { player.State.Set("toterKoerper", KoerperVon(w), true); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Körpertyp setzen: {e.Message}"); }
        }

        private static string KoerperVon(Werte w)
        {
            return Config.KoerperTyp(w.Stat["kraft"], w.Stat["ausdauer"], w.Stat["flexibilitaet"], w.Stat["kondition"]);
        }

        [EventHandler("toter-fitness:server:verlauf")]
        private async void OnVerlauf([FromSource] Player player, string stat)
        {
            if (Gedrosselt(player, "OnVerlauf", 500)) return;
            try
            {
                var cid = CitizenId(player);
                if (cid == null || !Config.Stats.Contains(stat)) return;
                var ab = TagesStart() - (Config.VerlaufTage - 1) * 86400L;
                var zeilen = await Abfrage($"SELECT tag, `{stat}` AS wert FROM fitness_verlauf WHERE citizenid = ? AND tag >= ? ORDER BY tag", cid, ab);
                TriggerClientEvent(player, "toter-fitness:client:verlauf", stat, zeilen.Select(z => (object)new Dictionary<string, object>
                {
                    { "tag", Datum(GrosseZahl(z, "tag") + 12 * 3600).Substring(0, 5) }, { "wert", Zahl(z, "wert") }
                }).ToList());
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Verlauf: {e.Message}");
            }
        }

        [EventHandler("toter-fitness:server:statsHolen")]
        private async void OnStatsHolen([FromSource] Player player, object oeffnen)
        {
            if (Gedrosselt(player, "OnStatsHolen", 100)) return;
            try
            {
                var cid = CitizenId(player);
                if (cid == null) return;
                var w = await WerteHolen(cid, player);
                StatsSenden(player, w, oeffnen is bool b && b, Convert.ToString(oeffnen) == "app");
                Begruessen(player, w);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Stats holen fehlgeschlagen: {e}");
            }
        }

        [EventHandler("playerDropped")]
        private void OnPlayerDropped([FromSource] Player player, string grund)
        {
            foreach (var k in drossel.Keys.Where(k => k.StartsWith(player.Handle + ":")).ToList()) drossel.Remove(k);
            Sitzung weg;
            if (sitzungen.TryGetValue(player.Handle, out weg)) CoachBeenden(weg);
            sitzungen.Remove(player.Handle);
            begruesst.Remove(player.Handle);
            var cid = CitizenId(player);
            if (cid != null) werte.Remove(cid);
        }

        [EventHandler("toter-fitness:server:start")]
        private async void OnStart([FromSource] Player player, object stationIdRoh)
        {
            if (Gedrosselt(player, "OnStart", 500)) return;
            try
            {
                var stationId = Convert.ToInt32(stationIdRoh);
                var station = stationen.FirstOrDefault(s => s.Id == stationId) ?? kursStationen.FirstOrDefault(s => s.Id == stationId);
                var cid = CitizenId(player);
                if (station == null || cid == null) return;
                if (sitzungen.ContainsKey(player.Handle))
                {
                    Melden(player, "Du trainierst schon.", "error");
                    return;
                }
                if (station.KursId != 0)
                {
                    var p = Position(player);
                    if (Abstand(p[0], p[1], p[2], station.X, station.Y, station.Z) > Config.InteraktionAbstand + 2f) return;
                    if (!KursPlatzNehmen(player, station, cid)) return;
                    await TrainingBeginnen(player, station, true);
                    return;
                }
                await TrainingBeginnen(player, station, false);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Training starten fehlgeschlagen: {e}");
            }
        }

        // Prüft Tageslimit, Pause, Mitgliedschaft bzw. Preis (nicht bei Kursen) und startet die Übung.
        private async Task TrainingBeginnen(Player player, Station station, bool kurs)
        {
            try
            {
                var cid = CitizenId(player);
                var uebung = Config.UebungFuer(station.Stat, station.Art);
                if (cid == null || uebung == null) return;

                if (sitzungen.ContainsKey(player.Handle))
                {
                    Melden(player, "Du trainierst schon.", "error");
                    return;
                }

                foreach (var alt in sitzungen.Where(x => Jetzt() - x.Value.Start > Config.BelegtMaxSekunden).Select(x => x.Key).ToList())
                {
                    sitzungen.Remove(alt);
                    int quelle;
                    if (int.TryParse(alt, out quelle) && SpielerVon(quelle) != null) TrainingStatus(SpielerVon(quelle), false);
                }
                if (!uebung.Route && sitzungen.Values.Any(x => x.StationId == station.Id))
                {
                    Melden(player, "Die Station ist gerade belegt.", "error");
                    return;
                }

                var pos = Position(player);
                if (Abstand(pos[0], pos[1], pos[2], station.X, station.Y, station.Z) > Config.InteraktionAbstand + 2f)
                {
                    Melden(player, "Du bist zu weit von der Station entfernt.", "error");
                    return;
                }

                var w = await WerteHolen(cid, player);
                long bis;
                long letztes;
                var schonHeute = Config.EinmalProTag && !w.Freigegeben.Contains(station.Stat)
                    && w.LetztesStat.TryGetValue(station.Stat, out letztes) && letztes >= TagesStart();
                if (!kurs && w.Abklingen.TryGetValue(station.Stat, out bis) && bis > Jetzt())
                {
                    var rest = bis - Jetzt();
                    Melden(player, $"Deine Muskeln brauchen noch {rest / 60 + 1} Minuten Pause für {Config.StatNamen[station.Stat]}.", "error");
                    return;
                }
                if (schonHeute && !kurs)
                {
                    Melden(player, $"{Config.StatNamen[station.Stat]} hast du heute schon trainiert. Ab {Config.TagesResetStunde}:00 Uhr geht es wieder.", "error");
                    return;
                }

                var gym = station.Art == "studio" && station.GymId > 0 ? GymName(station.GymId) : null;
                if (kurs) { }
                else if (gym != null)
                {
                    var karte = GueltigeKarte(player, cid, station.GymId);
                    if (karte == null)
                    {
                        Melden(player, $"Nur für Mitglieder von {gym}. Du brauchst eine gültige Mitgliedskarte.", "error");
                        return;
                    }
                }
                else if (station.Art == "studio" && !Bezahlen(player, Config.PreisStudio))
                {
                    Melden(player, $"Das Training im Studio kostet ${Config.PreisStudio}. Dafür reicht dein Konto nicht.", "error");
                    return;
                }

                var token = zufall.Next(100000, 999999).ToString();
                sitzungen[player.Handle] = new Sitzung
                {
                    StationId = station.Id, Stat = station.Stat, Art = station.Art, Token = token, Start = Jetzt(),
                    Nachschlag = kurs && schonHeute,
                    Leitung = kurs && kurse.FirstOrDefault(k => k.Id == station.KursId)?.LeiterCid == cid
                };
                CoachVerbinden(player, sitzungen[player.Handle]);
                TrainingStatus(player, true);
                if (station.Art == "studio" && gym == null) Melden(player, $"${Config.PreisStudio} für das Studio bezahlt.", "success");
                TriggerClientEvent(player, "toter-fitness:client:start", station.Id, token, sitzungen[player.Handle].Leitung);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Training beginnen fehlgeschlagen: {e}");
            }
        }

        private static void TrainingStatus(Player player, bool an)
        {
            try { player.State.Set("toterFitness", an, true); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Status setzen: {e.Message}"); }
        }

        [EventHandler("toter-fitness:server:abbruch")]
        private void OnAbbruch([FromSource] Player player, object token)
        {
            Sitzung s;
            if (sitzungen.TryGetValue(player.Handle, out s) && s.Token == Convert.ToString(token))
            {
                sitzungen.Remove(player.Handle);
                TrainingStatus(player, false);
                CoachBeenden(s);
            }
        }

        [EventHandler("toter-fitness:server:ergebnis")]
        private async void OnErgebnis([FromSource] Player player, object token, object punkteRoh, object rekordRoh)
        {
            try
            {
                Sitzung s;
                if (!sitzungen.TryGetValue(player.Handle, out s) || s.Token != Convert.ToString(token)) return;
                sitzungen.Remove(player.Handle);
                TrainingStatus(player, false);
                var coach = s.Coach != null ? SpielerVon(int.Parse(s.Coach)) : null;
                CoachBeenden(s);

                var cid = CitizenId(player);
                if (cid == null) return;
                var w = await WerteHolen(cid);

                var punkte = Math.Max(0, Math.Min(100, Convert.ToInt32(punkteRoh)));
                if (Jetzt() - s.Start < Config.MindestDauerSekunden) punkte = 0;
                if (s.Leitung) punkte = 100;

                var station = stationen.FirstOrDefault(x => x.Id == s.StationId) ?? kursStationen.FirstOrDefault(x => x.Id == s.StationId);
                if (station != null)
                {
                    var pos = Position(player);
                    var punkteDerStrecke = new List<float[]>(station.Route) { new[] { station.X, station.Y, station.Z } };
                    var naechster = punkteDerStrecke.Min(p => Abstand(p[0], p[1], p[2], pos[0], pos[1], pos[2]));
                    var erlaubt = Config.UebungFuer(station.Stat, station.Art)?.Route == true ? 40f : 10f;
                    if (naechster > erlaubt)
                    {
                        Melden(player, "Du warst nicht mehr an der Station, das Training zählt nicht.", "error");
                        return;
                    }
                }

                var maximal = s.Art == "studio" || Config.IstKurs(s.Art) ? Config.ZugewinnStudio : Config.ZugewinnFrei;
                var zugewinn = (int)Math.Round(maximal * punkte / 100.0);
                if (punkte >= 25 && zugewinn < 1) zugewinn = 1;
                var geschafft = zugewinn > 0;
                if (s.Nachschlag) zugewinn = 0;

                var boni = new List<string>();
                var shake = w.ShakeBis > Jetzt();
                Buchung buchung;
                buchungen.TryGetValue(player.Handle, out buchung);
                var reichweite = buchung != null && buchung.KursId > 0 ? (Config.UebungFuer(s.Stat, s.Art)?.Route == true ? 120f : Config.KursRadius + 5f) : Config.TrainerAbstand + 10f;
                var trainerBonus = 0;
                var kursBonus = 0;
                var basis = zugewinn;
                if (zugewinn > 0 && shake)
                {
                    zugewinn += Config.ShakePunkte;
                    boni.Add($"Shake +{Config.ShakePunkte}");
                }
                if (coach != null && buchung != null && buchung.Trainer == coach.Handle && Nah(coach, player, reichweite))
                {
                    buchung.Einheiten++;
                    if (basis > 0)
                    {
                        trainerBonus = Config.TrainerPunkte;
                        int bisher;
                        buchung.Bonus.TryGetValue(s.Stat, out bisher);
                        buchung.Bonus[s.Stat] = bisher + trainerBonus;
                    }
                    if (buchung.KursId > 0 && geschafft)
                    {
                        kursBonus = Config.KursBonus;
                        int bisher;
                        buchung.Bonus.TryGetValue(s.Stat, out bisher);
                        buchung.Bonus[s.Stat] = bisher + kursBonus;
                    }
                    if (buchung.KursId == 0) Melden(coach, $"{Charname(player)} ist mit der Übung fertig ({punkte} %).", "success");
                }

                if (!Config.IstKurs(s.Art)) await RekordPruefen(player, cid, s.Stat + ":" + s.Art, rekordRoh);

                var name = Config.StatNamen[s.Stat];
                var gratis = buchung != null && buchung.KursId > 0 && (kurse.FirstOrDefault(k => k.Id == buchung.KursId)?.Preis ?? 0) == 0;
                var wann = gratis ? " kommt gleich dazu, der Kurs ist kostenlos." : ", wenn du die Rechnung bezahlst.";
                if (s.Nachschlag)
                {
                    if (kursBonus > 0)
                        Melden(player, $"{name} hast du heute schon trainiert. Kurs-Bonus +{kursBonus}{wann}", "success");
                    else
                        Melden(player, punkte < 25
                            ? $"Nicht geschafft ({punkte} %), dafür gibt es keinen Kurs-Bonus."
                            : $"{name} hast du heute schon trainiert, der Kurs zählt ohne Leitung in der Nähe nicht.", "error");
                    if (buchung != null && buchung.KursId > 0) await KursAbrechnen(player, buchung);
                    return;
                }

                var alt = w.Stat[s.Stat];
                w.Stat[s.Stat] = Begrenzen(alt + zugewinn);
                w.Einheiten++;
                w.LetztesTraining = Jetzt();
                w.LetztesStat[s.Stat] = Jetzt();
                w.Freigegeben.Remove(s.Stat);
                w.Abklingen[s.Stat] = Jetzt() + Config.AbklingzeitSekunden;
                MuskelkaterPruefen(player, w);
                await Speichern(cid, w);

                var extra = boni.Count > 0 ? $" Mit {string.Join(" und ", boni)}." : "";
                if (trainerBonus + kursBonus > 0)
                    extra += kursBonus > 0
                        ? $" Trainer- und Kurs-Bonus +{trainerBonus + kursBonus}{wann}"
                        : $" Trainer-Bonus +{trainerBonus} gibt es, wenn du die Rechnung bezahlst.";
                if (w.Stat[s.Stat] > alt)
                    Melden(player, $"{name} +{w.Stat[s.Stat] - alt} (jetzt {w.Stat[s.Stat]}), {punkte} % der Übung.{extra}", "success");
                else if (alt >= Config.StatMax)
                    Melden(player, $"{name} ist schon auf dem Maximum.", "primary");
                else
                    Melden(player, $"Nicht geschafft ({punkte} %). {name} bleibt bei {alt}.", "error");
                StatsSenden(player, w, false);
                if (buchung != null && buchung.KursId > 0) await KursAbrechnen(player, buchung);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Ergebnis verarbeiten fehlgeschlagen: {e}");
            }
        }
    }
}
