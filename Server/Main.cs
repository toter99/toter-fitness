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
    public class Werte
    {
        public Dictionary<string, int> Stat = new Dictionary<string, int>();
        public long LetztesTraining;
        public long LetzterAbbau;
        public int Einheiten;
        public Dictionary<string, long> Abklingen = new Dictionary<string, long>();
        public Dictionary<string, long> LetztesStat = new Dictionary<string, long>();
        public Dictionary<string, long> AbbauBis = new Dictionary<string, long>();
        public HashSet<string> Freigegeben = new HashSet<string>();
        public long ShakeBis, VitamineBis;
        public long MuskelkaterAb, MuskelkaterBis;
    }

    public class Sitzung
    {
        public int StationId;
        public string Stat;
        public string Art;
        public string Token;
        public long Start;
        public string Coach;
        public string CoachName;
        public bool Nachschlag;     // Kurs, obwohl heute schon trainiert: nur Kurs-Bonus
        public bool Leitung;        // Kursleitung: Übung zählt immer als geschafft
    }

    public partial class Main : BaseScript
    {
        private readonly List<Station> stationen = new List<Station>();
        private readonly Dictionary<string, Werte> werte = new Dictionary<string, Werte>();
        private readonly Dictionary<string, Sitzung> sitzungen = new Dictionary<string, Sitzung>();
        private readonly Random zufall = new Random();

        // Schutz gegen Event-Spam (z. B. Datenbank-Abfragen im Sekundentakt): pro Spieler und Event eine Mindestpause.
        private readonly Dictionary<string, long> drossel = new Dictionary<string, long>();

        private bool Gedrosselt(Player player, string was, int ms)
        {
            if (player == null) return true;
            var schluessel = player.Handle + ":" + was;
            long jetzt = GetGameTimer();   // auf dem Server long, im Client int
            long zuletzt;
            if (drossel.TryGetValue(schluessel, out zuletzt) && jetzt - zuletzt < ms) return true;
            drossel[schluessel] = jetzt;
            return false;
        }

        public Main()
        {
            var version = GetResourceMetadata(Config.Ressource, "version", 0) ?? "";
            Debug.WriteLine($"^5{Config.Ressource}^7 {version} von {Config.Autor} · {Config.Projektseite}");
            Exports.Add("GetStats", new Func<string, object>(cid =>
            {
                Werte w;
                return werte.TryGetValue(cid ?? "", out w) ? new Dictionary<string, int>(w.Stat) : null;
            }));

            Exports.Add("Koerper", new Func<int, string>(quelle => KoerperTypVon(quelle)));
            Exports.Add("KannTragen", new Func<int, int, bool>((traeger, ziel) => Config.KannTragen(KoerperTypVon(traeger), KoerperTypVon(ziel))));

            Vorbereiten();
        }

        private async void Vorbereiten()
        {
            try { await GymsVorbereiten(); await ExtrasVorbereiten(); await VerlaufVorbereiten(); await KurseLaden(); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Gyms vorbereiten: {e}"); }
            StationenLaden();
        }

        private string KoerperTypVon(int quelle)
        {
            var player = SpielerVon(quelle);
            if (player == null) return "normal";
            var cid = CitizenId(player);
            Werte w;
            if (cid == null || !werte.TryGetValue(cid, out w)) return "normal";
            return KoerperVon(w);
        }

        private void Chat(Player player, string text)
        {
            TriggerClientEvent(player, "toter-fitness:client:ausgabe", text);
        }

        private Player SpielerVon(int quelle)
        {
            return quelle > 0 ? Players[quelle] : null;
        }

        private float[] Position(Player player)
        {
            var ped = GetPlayerPed(player.Handle);
            var p = GetEntityCoords(ped);
            return new[] { p.X, p.Y, p.Z, GetEntityHeading(ped) };
        }

        private static float Abstand(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            var dx = x1 - x2; var dy = y1 - y2; var dz = z1 - z2;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private Station StationMitId(List<object> args, Player player)
        {
            int id;
            if (args.Count < 1 || !int.TryParse(args[0].ToString(), out id))
            {
                Chat(player, "Keine Station gewählt.");
                return null;
            }
            var s = stationen.FirstOrDefault(x => x.Id == id);
            if (s == null) Chat(player, $"Station {id} gibt es nicht.");
            return s;
        }

        private async void StationenLaden()
        {
            try
            {
                var zeilen = await Abfrage("SELECT * FROM fitness_stations ORDER BY id");
                stationen.Clear();
                foreach (var z in zeilen)
                {
                    var stat = Text(z, "stat_type");
                    if (!Config.Stats.Contains(stat)) continue;
                    var art = Config.ArtAusDb(Text(z, "station_type"));
                    var uebung = Config.UebungFuer(stat, art);
                    var prop = Text(z, "prop_model");
                    stationen.Add(new Station
                    {
                        Id = Zahl(z, "id"),
                        Name = Text(z, "name"),
                        Stat = stat,
                        Art = art,
                        Prop = prop == "-" ? "" : (string.IsNullOrEmpty(prop) && uebung != null ? uebung.Prop : prop),
                        X = Komma(z, "position_x"),
                        Y = Komma(z, "position_y"),
                        Z = Komma(z, "position_z"),
                        Heading = Komma(z, "heading"),
                        Hoehe = Komma(z, "prop_height"),
                        Route = Station.RouteLesen(Text(z, "route")),
                        GymId = Zahl(z, "gym_id")
                    });
                    var neu = stationen[stationen.Count - 1];
                    if (uebung != null && uebung.Route) StartpunktSichern(neu);
                }
                Debug.WriteLine($"[{Config.Ressource}] {stationen.Count} Stationen geladen.");
                TriggerClientEvent("toter-fitness:client:stationen", StationenFuerClient());
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Stationen laden fehlgeschlagen: {e}");
            }
        }

        private static void StartpunktSichern(Station s)
        {
            if (s.Route.Count > 0 && Abstand(s.Route[0][0], s.Route[0][1], s.Route[0][2], s.X, s.Y, s.Z) < 3f)
            {
                s.Route[0] = new[] { s.X, s.Y, s.Z };
                return;
            }
            s.Route.Insert(0, new[] { s.X, s.Y, s.Z });
        }

        private List<object> StationenFuerClient()
        {
            return stationen.Concat(kursStationen).Select(s => (object)s.FuerClient()).ToList();
        }

        [EventHandler("toter-fitness:server:stationenHolen")]
        private void OnStationenHolen([FromSource] Player player)
        {
            if (Gedrosselt(player, "OnStationenHolen", 1000)) return;
            GymsSenden(player);
            TriggerClientEvent(player, "toter-fitness:client:stationen", StationenFuerClient());
        }

        private async void BefehlSetzen(int quelle, List<object> args, string roh)
        {
            var player = SpielerVon(quelle);
            if (player == null) return;
            try
            {
                var stat = args.Count > 0 ? args[0].ToString().ToLowerInvariant() : "";
                var art = args.Count > 1 ? args[1].ToString().ToLowerInvariant() : "";
                if (stat == "flexibilität") stat = "flexibilitaet";
                var uebung = Config.UebungFuer(stat, art);
                if (uebung == null)
                {
                    Chat(player, "Übung oder Art fehlt.");
                    return;
                }
                var prop = args.Count > 2 ? args[2].ToString() : "";
                var pos = Position(player);
                var name = uebung.Name + (art == "studio" ? " (Studio)" : "");
                var cid = CitizenId(player) ?? "";
                var id = await Sql("insert",
                    "INSERT INTO fitness_stations (name, stat_type, station_type, position_x, position_y, position_z, heading, prop_model, prop_height, created_by, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0, ?, ?)",
                    name, stat, Config.ArtZuDb(art), pos[0], pos[1], pos[2], pos[3], prop, cid, Jetzt());
                Chat(player, $"Station {id} gesetzt: {name}. Höhe und Drehung lassen sich unter \"Stationen in der Nähe\" anpassen."
                    + (uebung.Route ? " Der erste Streckenpunkt ist die Station selbst. Für weitere Punkte hinlaufen und dort \"+ Punkt\" drücken." : ""));
                StationenLaden();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnessplace: {e}");
            }
        }

        private async void BefehlLoeschen(int quelle, List<object> args, string roh)
        {
            var player = SpielerVon(quelle);
            if (player == null) return;
            try
            {
                var s = StationMitId(args, player);
                if (s == null) return;
                await Sql("update", "DELETE FROM fitness_stations WHERE id = ?", s.Id);
                Chat(player, $"Station {s.Id} ({s.Name}) gelöscht.");
                StationenLaden();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnessdelete: {e}");
            }
        }

        private async void BefehlVerschieben(int quelle, List<object> args, string roh)
        {
            var player = SpielerVon(quelle);
            if (player == null) return;
            try
            {
                var s = StationMitId(args, player);
                if (s == null) return;
                var pos = Position(player);
                await Sql("update", "UPDATE fitness_stations SET position_x = ?, position_y = ?, position_z = ?, heading = ? WHERE id = ?",
                    pos[0], pos[1], pos[2], pos[3], s.Id);
                Chat(player, $"Station {s.Id} an deine Position verschoben.");
                StationenLaden();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnessmove: {e}");
            }
        }

        private async void BefehlHoehe(int quelle, List<object> args, string roh)
        {
            var player = SpielerVon(quelle);
            if (player == null) return;
            try
            {
                var s = StationMitId(args, player);
                if (s == null) return;
                float hoehe;
                if (args.Count < 2 || !float.TryParse(args[1].ToString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out hoehe))
                {
                    Chat(player, $"Ungültige Höhe (aktuell {s.Hoehe:0.00} m).");
                    return;
                }
                await Sql("update", "UPDATE fitness_stations SET prop_height = ? WHERE id = ?", hoehe, s.Id);
                Chat(player, $"Prop von Station {s.Id} auf {hoehe:0.00} m Höhe gesetzt.");
                StationenLaden();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnesshoehe: {e}");
            }
        }

        private async void BefehlDrehen(int quelle, List<object> args, string roh)
        {
            var player = SpielerVon(quelle);
            if (player == null) return;
            try
            {
                var s = StationMitId(args, player);
                if (s == null) return;
                float grad;
                if (args.Count < 2 || !float.TryParse(args[1].ToString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out grad))
                {
                    Chat(player, "Ungültiger Winkel.");
                    return;
                }
                var neu = ((s.Heading + grad) % 360f + 360f) % 360f;
                await Sql("update", "UPDATE fitness_stations SET heading = ? WHERE id = ?", neu, s.Id);
                Chat(player, $"Station {s.Id} gedreht, Ausrichtung jetzt {neu:0}°.");
                StationenLaden();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnessdrehen: {e}");
            }
        }

        private async void BefehlWerteSetzen(int quelle, List<object> args, string roh)
        {
            var admin = SpielerVon(quelle);
            if (admin == null) return;
            try
            {
                var stat = args.Count > 0 ? args[0].ToString().ToLowerInvariant() : "";
                if (stat == "flexibilität") stat = "flexibilitaet";
                int wert;
                if ((stat != "alle" && !Config.Stats.Contains(stat)) || args.Count < 2 || !int.TryParse(args[1].ToString(), out wert))
                {
                    Chat(admin, "Wert oder Spieler fehlt.");
                    return;
                }
                var ziel = admin;
                int id;
                if (args.Count > 2 && int.TryParse(args[2].ToString(), out id))
                {
                    ziel = SpielerVon(id);
                    if (ziel == null || string.IsNullOrEmpty(ziel.Name))
                    {
                        Chat(admin, $"Spieler {id} ist nicht online.");
                        return;
                    }
                }
                var cid = CitizenId(ziel);
                if (cid == null)
                {
                    Chat(admin, "Der Spieler hat noch keinen Charakter geladen.");
                    return;
                }
                var w = await WerteHolen(cid);
                foreach (var s in Config.Stats)
                    if (stat == "alle" || s == stat) w.Stat[s] = Begrenzen(wert);
                w.Abklingen.Clear();
                foreach (var s in Config.Stats) w.Freigegeben.Add(s);
                await Speichern(cid, w);
                StatsSenden(ziel, w, false);
                Chat(admin, $"{(stat == "alle" ? "Alle Werte" : Config.StatNamen[stat])} von {ziel.Name} auf {Begrenzen(wert)} gesetzt.");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnessset: {e}");
            }
        }

        private async void BefehlRoute(int quelle, List<object> args, string roh)
        {
            var player = SpielerVon(quelle);
            if (player == null) return;
            try
            {
                var s = StationMitId(args, player);
                if (s == null) return;
                var aktion = args.Count > 1 ? args[1].ToString().ToLowerInvariant() : "";
                if (aktion == "show")
                {
                    TriggerClientEvent(player, "toter-fitness:client:routeZeigen", s.Id);
                    return;
                }
                if (aktion == "add")
                {
                    var pos = Position(player);
                    s.Route.Add(new[] { pos[0], pos[1], pos[2] });
                }
                else if (aktion == "undo")
                {
                    if (s.Route.Count > 1) s.Route.RemoveAt(s.Route.Count - 1);
                }
                else if (aktion == "clear")
                {
                    s.Route.Clear();
                }
                else
                {
                    Chat(player, "Unbekannte Aktion für die Strecke.");
                    return;
                }
                StartpunktSichern(s);
                await Sql("update", "UPDATE fitness_stations SET route = ? WHERE id = ?", s.RouteText(), s.Id);
                Chat(player, $"Strecke von Station {s.Id} hat jetzt {s.Route.Count} Punkte.");
                StationenLaden();
                TriggerClientEvent(player, "toter-fitness:client:routeZeigen", s.Id);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] fitnessroute: {e}");
            }
        }
    }
}
