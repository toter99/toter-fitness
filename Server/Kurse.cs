using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Server
{
    public partial class Main
    {
        private class Kurs
        {
            public int Id, GymId, Dauer, Max, RouteStation, Preis;
            public string Titel, Ort, Leiter, LeiterCid, Uebung;
            public float X, Y, Z, Richtung;
            public long Start;
            public HashSet<string> Teilnehmer = new HashSet<string>();
            public HashSet<int> Erinnert = new HashSet<int>();
            public HashSet<string> Abgerechnet = new HashSet<string>();
            public bool Laeuft;
            public readonly Dictionary<string, int> Bereit = new Dictionary<string, int>();  // citizenid -> Station

            public long Ende => Start + Dauer * 60L;
            public string Stat => Uebung.Split(':')[0];
            public string Art => Uebung.Split(':')[1];
            public bool Joggen => Uebung == "ausdauer:kurs-joggen";
        }

        private readonly List<Kurs> kurse = new List<Kurs>();
        private readonly List<Station> kursStationen = new List<Station>();
        private readonly HashSet<string> begruesst = new HashSet<string>();
        private int kursStationNummer = -1000;

        private async Task VerlaufVorbereiten()
        {
            await Sql("update", "CREATE TABLE IF NOT EXISTS fitness_verlauf (citizenid VARCHAR(50) NOT NULL, tag BIGINT NOT NULL, kraft INT NOT NULL, " +
                "ausdauer INT NOT NULL, flexibilitaet INT NOT NULL, kondition INT NOT NULL, PRIMARY KEY (citizenid, tag))");
            await Sql("update", "DELETE FROM fitness_verlauf WHERE tag < ?", Jetzt() - (Config.VerlaufTage + 30) * 86400L);
            await Sql("update", "CREATE TABLE IF NOT EXISTS fitness_probetage (citizenid VARCHAR(50) NOT NULL, gym_id INT NOT NULL, zeit BIGINT NOT NULL, " +
                "PRIMARY KEY (citizenid, gym_id))");
        }

        private async Task KurseLaden()
        {
            await Sql("update", "CREATE TABLE IF NOT EXISTS fitness_kurse (id INT AUTO_INCREMENT PRIMARY KEY, gym_id INT NOT NULL, titel VARCHAR(60) NOT NULL, " +
                "ort VARCHAR(80) NOT NULL DEFAULT '', x FLOAT NOT NULL, y FLOAT NOT NULL, z FLOAT NOT NULL, start BIGINT NOT NULL, dauer INT NOT NULL, " +
                "leiter VARCHAR(80) NOT NULL DEFAULT '', leiter_cid VARCHAR(60) NOT NULL DEFAULT '', teilnehmer TEXT)");
            foreach (var spalte in new[] { "uebung VARCHAR(40) NOT NULL DEFAULT 'flexibilitaet:kurs-yoga'", "max INT NOT NULL DEFAULT 10",
                "richtung FLOAT NOT NULL DEFAULT 0", "route INT NOT NULL DEFAULT 0",
                "preis INT NOT NULL DEFAULT 0" })
                await Sql("update", "ALTER TABLE fitness_kurse ADD COLUMN IF NOT EXISTS " + spalte);
            await Sql("update", "DELETE FROM fitness_kurse WHERE start + dauer * 60 < ?", Jetzt() - 3600);
            kurse.Clear();
            foreach (var z in await Abfrage("SELECT * FROM fitness_kurse"))
            {
                var k = new Kurs
                {
                    Id = Zahl(z, "id"), GymId = Zahl(z, "gym_id"), Titel = Text(z, "titel"), Ort = Text(z, "ort"),
                    X = Komma(z, "x"), Y = Komma(z, "y"), Z = Komma(z, "z"), Start = GrosseZahl(z, "start"), Dauer = Zahl(z, "dauer"),
                    Leiter = Text(z, "leiter"), LeiterCid = Text(z, "leiter_cid"), Uebung = Text(z, "uebung"), Max = Zahl(z, "max", 10),
                    Richtung = Komma(z, "richtung"), RouteStation = Zahl(z, "route"), Preis = Zahl(z, "preis")
                };
                if (!Config.KursArten.Contains(k.Uebung)) k.Uebung = Config.KursArten[0];
                foreach (var cid in Text(z, "teilnehmer").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) k.Teilnehmer.Add(cid);
                foreach (var m in Config.KursErinnerungen) if (k.Start - Jetzt() < m * 60) k.Erinnert.Add(m);
                kurse.Add(k);
            }
            Tick += KursTick;
        }

        private static long BerlinZuUnix(DateTime berlin)
        {
            return (long)(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(berlin, DateTimeKind.Unspecified), Zeitzone()) - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private static string KursZeit(long unix)
        {
            var b = Berlin(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix));
            var heute = Berlin(DateTime.UtcNow).Date;
            var tag = b.Date == heute ? "Heute" : b.Date == heute.AddDays(1) ? "Morgen" : b.ToString("ddd dd.MM.", new CultureInfo("de-DE"));
            return $"{tag} {b:HH:mm}";
        }

        private Gym MitarbeiterGym(Player player)
        {
            var job = JobVon(player);
            if (job.Name == "") return null;
            return gyms.FirstOrDefault(x => !string.IsNullOrEmpty(x.Job) && x.Job == job.Name);
        }

        // Trainer oder Chef des Gyms (Rang ab RangTrainer), darf planen.
        private Gym KursGym(Player player)
        {
            var g = MitarbeiterGym(player);
            if (g == null) return null;
            var job = JobVon(player);
            return job.Rang >= Config.RangTrainer || job.Chef ? g : null;
        }

        private bool DarfLeiten(Player player, Kurs k)
        {
            var g = KursGym(player);
            return g != null && g.Id == k.GymId && (CitizenId(player) == k.LeiterCid || JobVon(player).Chef);
        }

        private List<object> KurseDaten(Player player)
        {
            var cid = CitizenId(player);
            var meinGym = MitarbeiterGym(player);
            return kurse.Where(k => k.Ende > Jetzt() && k.Start < Jetzt() + 7 * 86400L).OrderBy(k => k.Start).Select(k =>
            {
                var u = Config.Uebungen[k.Uebung];
                var route = k.Joggen ? stationen.FirstOrDefault(s => s.Id == k.RouteStation) : null;
                return (object)new Dictionary<string, object>
                {
                    { "id", k.Id }, { "titel", k.Titel }, { "gym", gyms.FirstOrDefault(g => g.Id == k.GymId)?.Name ?? "" }, { "gymId", k.GymId },
                    { "uebung", u.Name }, { "schluessel", k.Uebung }, { "stat", k.Stat }, { "joggen", k.Joggen }, { "preis", k.Preis },
                    { "kx", k.X }, { "ky", k.Y }, { "kz", k.Z },
                    { "ort", k.Ort }, { "zeit", KursZeit(k.Start) }, { "dauer", k.Dauer }, { "leiter", k.Leiter },
                    { "laeuft", k.Laeuft }, { "angemeldet", cid != null && k.Teilnehmer.Contains(cid) },
                    { "teilnehmer", k.Teilnehmer.Count }, { "max", k.Max }, { "bereit", k.Bereit.Count },
                    { "aufgebaut", kursStationen.Any(s => s.KursId == k.Id) },
                    { "matten", kursStationen.Count(s => s.KursId == k.Id) },
                    { "x", route?.X ?? k.X }, { "y", route?.Y ?? k.Y },
                    { "meiner", DarfLeiten(player, k) },
                    { "mitarbeiter", meinGym != null && meinGym.Id == k.GymId }
                };
            }).ToList();
        }

        private void KurseSenden(Player player)
        {
            TriggerClientEvent(player, "toter-fitness:client:kurse", KurseDaten(player));
        }

        private void KursStationenSenden()
        {
            TriggerClientEvent("toter-fitness:client:stationen", StationenFuerClient());
            KurseAnAlle();
        }

        // Nach jeder Änderung bekommt jeder die Kursliste neu (die Daten sind pro Spieler verschieden: angemeldet, meiner).
        private void KurseAnAlle()
        {
            foreach (var p in Players.ToList())
                if (CitizenId(p) != null) KurseSenden(p);
        }

        [EventHandler("toter-fitness:server:kurse")]
        private void OnKurse([FromSource] Player player)
        {
            if (Gedrosselt(player, "OnKurse", 100)) return;
            KurseSenden(player);
        }

        [EventHandler("toter-fitness:server:kursAnlegen")]
        private async void OnKursAnlegen([FromSource] Player player, object datenRoh)
        {
            if (Gedrosselt(player, "OnKursAnlegen", 2000)) return;
            try
            {
                var g = KursGym(player);
                if (g == null)
                {
                    Melden(player, "Kurse planen nur Trainer und Chefs.", "error");
                    KurseSenden(player);
                    return;
                }
                var d = AlsTabelle(datenRoh) ?? new Dictionary<string, object>();
                var titel = Text(d, "titel").Trim();
                var ort = Text(d, "ort").Trim();
                var uebung = Text(d, "uebung");
                if (ort.Length > 80) ort = ort.Substring(0, 80);
                if (titel.Length < 2 || titel.Length > 60 || !Config.KursArten.Contains(uebung))
                {
                    Melden(player, "Der Kurs braucht einen Namen (bis 60 Zeichen) und eine Übung.", "error");
                    KurseSenden(player);
                    return;
                }
                var tag = Math.Max(0, Math.Min(6, Zahl(d, "tag")));
                TimeSpan uhrzeit;
                if (!TimeSpan.TryParseExact(Text(d, "uhrzeit"), "hh\\:mm", CultureInfo.InvariantCulture, out uhrzeit))
                {
                    Melden(player, "Uhrzeit bitte als HH:MM.", "error");
                    KurseSenden(player);
                    return;
                }
                var dauer = Math.Max(15, Math.Min(180, Zahl(d, "dauer", 60)));
                var max = Math.Max(1, Math.Min(Config.KursMaxTeilnehmer, Zahl(d, "max", 10)));
                var preis = Math.Max(0, Math.Min(Config.TrainerMaxPreis, Zahl(d, "preis")));
                var start = BerlinZuUnix(Berlin(DateTime.UtcNow).Date.AddDays(tag).Add(uhrzeit));
                if (start + dauer * 60L < Jetzt())
                {
                    Melden(player, "Dieser Zeitpunkt ist schon vorbei.", "error");
                    KurseSenden(player);
                    return;
                }
                if (kurse.Count(k => k.GymId == g.Id && k.Ende > Jetzt()) >= Config.KurseProGym)
                {
                    Melden(player, $"Euer Gym hat schon {Config.KurseProGym} Kurse geplant.", "error");
                    KurseSenden(player);
                    return;
                }
                var routeId = 0;
                if (uebung == "ausdauer:kurs-joggen")
                {
                    routeId = Zahl(d, "route");
                    var r = stationen.FirstOrDefault(s => s.Id == routeId && s.Art == "frei" && s.Route.Count > 0);
                    if (r == null)
                    {
                        Melden(player, "Für einen Lauf-Kurs bitte eine freie Joggingstrecke wählen.", "error");
                        KurseSenden(player);
                        return;
                    }
                }
                var p = Position(player);
                var k2 = new Kurs
                {
                    GymId = g.Id, Titel = titel, Ort = ort, X = p[0], Y = p[1], Z = p[2], Start = start, Dauer = dauer,
                    Leiter = Charname(player), LeiterCid = CitizenId(player) ?? "", Uebung = uebung, Max = max, Preis = preis,
                    Richtung = GetEntityHeading(GetPlayerPed(player.Handle)), RouteStation = routeId
                };
                var id = await Sql("insert", "INSERT INTO fitness_kurse (gym_id, titel, ort, x, y, z, start, dauer, leiter, leiter_cid, teilnehmer, uebung, max, richtung, route, preis) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, '', ?, ?, ?, ?, ?)",
                    k2.GymId, k2.Titel, k2.Ort, k2.X, k2.Y, k2.Z, k2.Start, k2.Dauer, k2.Leiter, k2.LeiterCid, k2.Uebung, k2.Max, k2.Richtung, k2.RouteStation, k2.Preis);
                k2.Id = Convert.ToInt32(id);
                foreach (var m in Config.KursErinnerungen) if (k2.Start - Jetzt() < m * 60) k2.Erinnert.Add(m);
                kurse.Add(k2);
                KurseAnAlle();
                MitgliederBenachrichtigen(k2, player);
                Melden(player, k2.Joggen
                    ? $"Lauf-Kurs „{k2.Titel}“ für {KursZeit(k2.Start)} geplant. Start ist am Beginn der gewählten Strecke."
                    : $"Kurs „{k2.Titel}“ für {KursZeit(k2.Start)} geplant. Die Matten legst du hier vor Ort über „Matten legen“.", "success");
                Debug.WriteLine($"[{Config.Ressource}] {player.Name} plant Kurs {k2.Titel} ({g.Name}, {k2.Uebung}, max {k2.Max}) für {Datum(k2.Start)}.");
                KurseSenden(player);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Kurs anlegen: {e.Message}");
            }
        }

        [EventHandler("toter-fitness:server:kursAbsagen")]
        private async void OnKursAbsagen([FromSource] Player player, int id)
        {
            if (Gedrosselt(player, "OnKursAbsagen", 1000)) return;
            var k = kurse.FirstOrDefault(x => x.Id == id);
            if (k == null || !DarfLeiten(player, k))
            {
                KurseSenden(player);
                return;
            }
            await KursEntfernen(k);
            foreach (var t in OnlineMit(k.Teilnehmer))
                HandyNachricht(t, "Kurs abgesagt", $"„{k.Titel}“ am {KursZeit(k.Start)} findet nicht statt.");
            Melden(player, $"Kurs „{k.Titel}“ abgesagt.", "primary");
            KurseSenden(player);
        }

        private async Task KursEntfernen(Kurs k)
        {
            kurse.Remove(k);
            if (kursStationen.RemoveAll(s => s.KursId == k.Id) > 0) KursStationenSenden();
            else KurseAnAlle();
            await Sql("update", "DELETE FROM fitness_kurse WHERE id = ?", k.Id);
        }

        [EventHandler("toter-fitness:server:kursAnmelden")]
        private async void OnKursAnmelden([FromSource] Player player, int id, bool dabei)
        {
            if (Gedrosselt(player, "OnKursAnmelden", 1000)) return;
            var k = kurse.FirstOrDefault(x => x.Id == id);
            var cid = CitizenId(player);
            if (k == null || cid == null || k.Ende < Jetzt())
            {
                KurseSenden(player);
                return;
            }
            if (dabei && !k.Teilnehmer.Contains(cid) && k.Teilnehmer.Count >= k.Max)
            {
                Melden(player, "Der Kurs ist voll.", "error");
                KurseSenden(player);
                return;
            }
            if (dabei) k.Teilnehmer.Add(cid);
            else
            {
                k.Teilnehmer.Remove(cid);
                k.Bereit.Remove(cid);
            }
            await Sql("update", "UPDATE fitness_kurse SET teilnehmer = ? WHERE id = ?", string.Join(";", k.Teilnehmer), k.Id);
            if (dabei) Melden(player, $"Angemeldet: „{k.Titel}“, {KursZeit(k.Start)}. Du bekommst vorher eine Nachricht.", "success");
            KurseAnAlle();
        }

        // ---------------------------------------------------------------- Aufbauen

        private Kurs KursZumAufbauen(Player player, int id, out Station ziel)
        {
            ziel = null;
            var k = kurse.FirstOrDefault(x => x.Id == id);
            var g = MitarbeiterGym(player);
            if (k == null || g == null || g.Id != k.GymId)
            {
                Melden(player, "Auf- und abbauen dürfen nur Mitarbeiter des Gyms.", "error");
                return null;
            }
            var p = Position(player);
            ziel = k.Joggen ? stationen.FirstOrDefault(s => s.Id == k.RouteStation) : null;
            float zx = ziel?.X ?? k.X, zy = ziel?.Y ?? k.Y, zz = ziel?.Z ?? k.Z;
            if (Abstand(p[0], p[1], p[2], zx, zy, zz) > Config.AufbauAbstand)
            {
                Melden(player, "Dafür musst du am Kursort sein.", "error");
                return null;
            }
            return k;
        }

        // Abbauen geht für jeden Mitarbeiter. Aufbauen per Server nur beim Lauf-Kurs (Startpunkt der Strecke),
        // Matten legen die Mitarbeiter selbst (kursMatten).
        [EventHandler("toter-fitness:server:kursAufbauen")]
        private void OnKursAufbauen([FromSource] Player player, int id, bool aufbauen)
        {
            if (Gedrosselt(player, "OnKursAufbauen", 1000)) return;
            Station ziel;
            var k = KursZumAufbauen(player, id, out ziel);
            if (k == null) return;
            if (aufbauen && !k.Joggen)
            {
                Melden(player, "Die Matten legst du selbst über „Matten legen“.", "error");
                return;
            }
            kursStationen.RemoveAll(s => s.KursId == k.Id);
            k.Bereit.Clear();
            if (!aufbauen)
            {
                KursStationenSenden();
                Melden(player, $"„{k.Titel}“ abgebaut.", "primary");
                KurseSenden(player);
                return;
            }
            if (ziel == null) return;
            var u = Config.Uebungen[k.Uebung];
            kursStationen.Add(new Station
            {
                Id = kursStationNummer--, Name = u.Name, Stat = k.Stat, Art = k.Art, Prop = "", X = ziel.X, Y = ziel.Y, Z = ziel.Z,
                Heading = ziel.Heading, GymId = k.GymId, KursId = k.Id, Platz = 0, Route = new List<float[]>(ziel.Route)
            });
            KursStationenSenden();
            Melden(player, $"Startpunkt für „{k.Titel}“ steht.", "success");
            KurseSenden(player);
        }

        // Matten, die ein Mitarbeiter vor Ort gelegt hat. Erste = Kursleitung. Je Matte x, y, z (Boden + 0.98), h.
        [EventHandler("toter-fitness:server:kursMatten")]
        private void OnKursMatten([FromSource] Player player, int id, List<object> matten)
        {
            if (Gedrosselt(player, "OnKursMatten", 1000)) return;
            try
            {
                Station ziel;
                var k = KursZumAufbauen(player, id, out ziel);
                if (k == null || k.Joggen || matten == null) return;
                if (k.Laeuft)
                {
                    Melden(player, "Der Kurs läuft schon.", "error");
                    return;
                }
                var liste = new List<float[]>();
                foreach (var roh in matten.Take(k.Max + 1))
                {
                    var m = AlsTabelle(roh);
                    if (m == null) continue;
                    float x = Komma(m, "x"), y = Komma(m, "y"), z = Komma(m, "z"), h = Komma(m, "h"), hoehe = Komma(m, "hoehe");
                    if (float.IsNaN(hoehe)) hoehe = 0f;
                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || float.IsNaN(h)) continue;
                    if (Abstand(x, y, z, k.X, k.Y, k.Z) > Config.KursRadius) continue;
                    if (liste.Any(a => Abstand(a[0], a[1], a[2], x, y, z) < Config.MattenMinAbstand * 0.8f)) continue;
                    liste.Add(new[] { x, y, z, ((h % 360f) + 360f) % 360f, Math.Max(-0.3f, Math.Min(1f, hoehe)) });
                }
                if (liste.Count < 1) return;
                kursStationen.RemoveAll(s => s.KursId == k.Id);
                k.Bereit.Clear();
                var u = Config.Uebungen[k.Uebung];
                for (var i = 0; i < liste.Count; i++)
                    kursStationen.Add(new Station
                    {
                        Id = kursStationNummer--, Name = u.Name, Stat = k.Stat, Art = k.Art, Prop = u.Prop,
                        X = liste[i][0], Y = liste[i][1], Z = liste[i][2], Heading = liste[i][3], Hoehe = liste[i][4],
                        GymId = k.GymId, KursId = k.Id, Platz = i, Leitung = i == 0
                    });
                KursStationenSenden();
                Melden(player, liste.Count - 1 >= k.Max
                    ? $"Alle {k.Max} Matten und deine Matte für „{k.Titel}“ liegen."
                    : $"{liste.Count - 1} von {k.Max} Matten liegen. Mit „Matten legen“ machst du später weiter.", "success");
                KurseSenden(player);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Matten legen: {e.Message}");
            }
        }

        // Nach der Übung im Kurs: Rechnung mit dem Kurspreis (Boni nach Bezahlung), kostenlos = Boni sofort.
        private async Task KursAbrechnen(Player kunde, Buchung b)
        {
            var k = kurse.FirstOrDefault(x => x.Id == b.KursId);
            var cid = CitizenId(kunde);
            if (k != null && cid != null) k.Abgerechnet.Add(cid);
            var trainer = SpielerVon(int.Parse(b.Trainer));
            if (b.Einheiten == 0 || trainer == null)
            {
                CoachingEnde(b, false);
                offeneBuchungen.Remove(b);
                return;
            }
            await Abschliessen(trainer, b, k?.Preis ?? 0);
        }

        // Wird aus OnStart gerufen, wenn jemand an einer Kurs-Matte E drückt. true = Training jetzt starten.
        private bool KursPlatzNehmen(Player player, Station station, string cid)
        {
            var k = kurse.FirstOrDefault(x => x.Id == station.KursId);
            if (k == null)
            {
                Melden(player, "Dieser Kurs findet nicht mehr statt.", "error");
                return false;
            }
            var leitung = cid == k.LeiterCid;
            if (station.Leitung && !leitung)
            {
                Melden(player, "Das ist die Matte der Kursleitung.", "error");
                return false;
            }
            if (!leitung && !k.Teilnehmer.Contains(cid))
            {
                Melden(player, "Nur für Angemeldete. Anmelden geht in der App Fitness.", "error");
                return false;
            }
            if (!k.Joggen && k.Bereit.Any(b => b.Value == station.Id && b.Key != cid))
            {
                Melden(player, "Diese Matte ist schon vergeben, nimm eine andere.", "error");
                return false;
            }
            if (k.Laeuft)
            {
                if (!leitung) KundeImKurs(player, k);
                return true;
            }
            k.Bereit[cid] = station.Id;
            Melden(player, leitung ? "Du bist bereit. Starte den Kurs im GymDesk, wenn alle da sind."
                                   : "Du bist bereit. Bleib hier, der Kurs startet für alle gleichzeitig.", "success");
            var leiter = OnlineMit(new[] { k.LeiterCid }).FirstOrDefault();
            if (leiter != null && !leitung) Melden(leiter, $"{Charname(player)} ist bereit ({k.Bereit.Count}/{k.Teilnehmer.Count + 1}).", "primary");
            return false;
        }

        private void KundeImKurs(Player kunde, Kurs k)
        {
            Buchung b;
            if (buchungen.TryGetValue(kunde.Handle, out b) && b.KursId == k.Id) return;
            var cid = CitizenId(kunde);
            if (cid != null && k.Abgerechnet.Contains(cid)) return;
            var leiter = OnlineMit(new[] { k.LeiterCid }).FirstOrDefault();
            if (leiter == null) return;
            if (b != null) CoachingEnde(b, false);
            buchungen[kunde.Handle] = new Buchung
            {
                Id = ++buchungNummer, Trainer = leiter.Handle, TrainerName = k.Leiter, Kunde = kunde.Handle, KundeName = Charname(kunde),
                Job = gyms.FirstOrDefault(g => g.Id == k.GymId)?.Job ?? "", Bis = k.Ende, KursId = k.Id, KursTitel = k.Titel
            };
        }

        [EventHandler("toter-fitness:server:kursStarten")]
        private async void OnKursStarten([FromSource] Player player, int id)
        {
            if (Gedrosselt(player, "OnKursStarten", 2000)) return;
            var k = kurse.FirstOrDefault(x => x.Id == id);
            if (k == null || !DarfLeiten(player, k))
            {
                KurseSenden(player);
                return;
            }
            if (!kursStationen.Any(s => s.KursId == k.Id))
            {
                Melden(player, "Erst aufbauen, dann starten.", "error");
                KurseSenden(player);
                return;
            }
            k.Laeuft = true;
            if (k.Start > Jetzt()) k.Start = Jetzt();
            var leiterCid = CitizenId(player);
            var reihenfolge = k.Bereit.OrderBy(b => b.Key == k.LeiterCid ? 0 : 1).ToList();
            var gestartet = 0;
            foreach (var b in reihenfolge)
            {
                var p = OnlineMit(new[] { b.Key }).FirstOrDefault();
                var st = kursStationen.FirstOrDefault(s => s.Id == b.Value);
                if (p == null || st == null) continue;
                var pos = Position(p);
                if (Abstand(pos[0], pos[1], pos[2], st.X, st.Y, st.Z) > Config.InteraktionAbstand + 2f)
                {
                    Melden(p, "Du warst beim Start nicht an deinem Platz. Geh hin und drück E, um nachzustarten.", "error");
                    continue;
                }
                if (b.Key != k.LeiterCid) KundeImKurs(p, k);
                if (k.Joggen && gestartet > 0) await Delay(gestartet == 1 && b.Key != k.LeiterCid ? 2500 : 1200);
                await TrainingBeginnen(p, st, true);
                gestartet++;
            }
            k.Bereit.Clear();
            foreach (var t in OnlineMit(k.Teilnehmer.Concat(new[] { k.LeiterCid })))
                if (t.Handle != player.Handle) HandyNachricht(t, k.Titel, "Der Kurs hat begonnen.");
            KurseAnAlle();
            Melden(player, $"„{k.Titel}“ läuft, {gestartet} {(gestartet == 1 ? "Person" : "Personen")} gestartet. Die Rechnungen gehen nach der Übung automatisch raus.", "success");
            KurseSenden(player);
        }

        private IEnumerable<Player> OnlineMit(IEnumerable<string> cids)
        {
            var liste = new HashSet<string>(cids);
            return Players.ToList().Where(p => { var c = CitizenId(p); return c != null && liste.Contains(c); });
        }

        private async Task KursTick()
        {
            await Delay(30000);
            try
            {
                foreach (var k in kurse.ToList())
                {
                    var leute = OnlineMit(k.Teilnehmer.Concat(new[] { k.LeiterCid })).ToList();
                    foreach (var m in Config.KursErinnerungen.OrderByDescending(x => x))
                    {
                        if (k.Erinnert.Contains(m) || k.Start - Jetzt() > m * 60) continue;
                        k.Erinnert.Add(m);
                        foreach (var p in leute)
                            HandyNachricht(p, k.Titel, $"Beginnt in {m} Minuten. Treffpunkt: {(k.Ort == "" ? "siehe App" : k.Ort)}.");
                    }
                    if (k.Ende < Jetzt() - 3600) await KursEntfernen(k);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Kurse prüfen: {e.Message}");
            }
        }

        // ---------------------------------------------------------------- Nachrichten beim Einloggen

        // Neuer Kurs: alle Spieler online mit gültiger Karte dieses Gyms bekommen eine Handy-Nachricht.
        private void MitgliederBenachrichtigen(Kurs k, Player planer)
        {
            var gym = gyms.FirstOrDefault(g => g.Id == k.GymId);
            if (gym == null) return;
            var jetzt = Jetzt();
            foreach (var p in Players.ToList())
            {
                if (p.Handle == planer.Handle) continue;
                var cid = CitizenId(p);
                if (cid == null) continue;
                if (!KartenVon(p).Any(c => c.GymId == k.GymId && c.CitizenId == cid && c.Ablauf > jetzt)) continue;
                HandyNachricht(p, $"Neuer Kurs bei {gym.Name}", $"„{k.Titel}“ ({Config.Uebungen[k.Uebung].Name}), {KursZeit(k.Start)}. Anmelden in der Fitness-App.");
            }
        }

        private void Begruessen(Player player, Werte w)
        {
            if (!begruesst.Add(player.Handle)) return;
            var jetzt = Jetzt();
            var cid = CitizenId(player);
            foreach (var karte in KartenVon(player).Where(k => k.CitizenId == cid && k.Ablauf > jetzt && k.Ablauf - jetzt < 86400))
                HandyNachricht(player, "Mitgliedschaft", $"Deine Karte für {karte.Gym} läuft am {Datum(karte.Ablauf)} ab.");
            if (HatMuskelkater(w))
                HandyNachricht(player, "Muskelkater", $"Heute bist du etwas langsamer, bis etwa {Datum(w.MuskelkaterBis).Substring(11)} Uhr. Muskelbalsam hilft.");
            var lange = Config.Stats.Where(st => w.LetztesStat.ContainsKey(st) && w.LetztesStat[st] > 0 && jetzt - w.LetztesStat[st] > Config.ErinnernNachTagen * 86400L && w.Stat[st] > Config.StatStart).ToList();
            if (lange.Count > 0)
                HandyNachricht(player, "Training", $"{string.Join(" und ", lange.Select(st => Config.StatNamen[st]))} {(lange.Count == 1 ? "sinkt" : "sinken")}, du warst seit {Config.ErinnernNachTagen} Tagen nicht dran.");
            var cidKurse = kurse.Where(k => k.Teilnehmer.Contains(cid) && k.Start > jetzt && k.Start - jetzt < 86400).ToList();
            foreach (var k in cidKurse) HandyNachricht(player, k.Titel, $"Du bist angemeldet: {KursZeit(k.Start)}.");
            var meineGyms = KartenVon(player).Where(c => c.CitizenId == cid && c.Ablauf > jetzt).Select(c => c.GymId).Distinct().ToList();
            var offen = kurse.Where(k => meineGyms.Contains(k.GymId) && !k.Teilnehmer.Contains(cid) && k.Start > jetzt && k.Start - jetzt < 2 * 86400).ToList();
            if (offen.Count > 0)
                HandyNachricht(player, "Kurse in deinem Gym", offen.Count == 1
                    ? $"„{offen[0].Titel}“, {KursZeit(offen[0].Start)}. Anmelden in der Fitness-App."
                    : $"{offen.Count} Kurse in den nächsten zwei Tagen. Anmelden in der Fitness-App.");
        }
    }
}
