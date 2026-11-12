using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Server
{
    public class Gym
    {
        public int Id;
        public string Name;
        public string Job = "";
        public int PreisTag, PreisWoche, PreisMonat;
        public float[] Empfang;
        public Dictionary<string, int> Shop = new Dictionary<string, int>();

        public int ShopPreis(Supplement s)
        {
            int p;
            return Shop.TryGetValue(s.Item, out p) ? p : s.Preis;
        }
    }

    public partial class Main
    {
        private readonly List<Gym> gyms = new List<Gym>();
        private static readonly CultureInfo De = new CultureInfo("de-DE");

        private async Task GymsVorbereiten()
        {
            await Sql("update", "CREATE TABLE IF NOT EXISTS `player_fitness` (`citizenid` VARCHAR(50) NOT NULL, " +
                "`kraft` INT UNSIGNED NOT NULL DEFAULT 50, `ausdauer` INT UNSIGNED NOT NULL DEFAULT 50, " +
                "`flexibilitaet` INT UNSIGNED NOT NULL DEFAULT 50, `kondition` INT UNSIGNED NOT NULL DEFAULT 50, " +
                "`last_training` INT UNSIGNED NOT NULL DEFAULT 0, `last_login` INT UNSIGNED NOT NULL DEFAULT 0, " +
                "`last_abbau` INT UNSIGNED NOT NULL DEFAULT 0, `total_training_sessions` INT UNSIGNED NOT NULL DEFAULT 0, " +
                "PRIMARY KEY (`citizenid`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            await Sql("update", "CREATE TABLE IF NOT EXISTS `fitness_stations` (`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY, " +
                "`name` VARCHAR(100) NOT NULL, `stat_type` ENUM('kraft','ausdauer','flexibilitaet','kondition') NOT NULL, " +
                "`station_type` ENUM('kostenlos','gym','outdoor') NOT NULL DEFAULT 'kostenlos', " +
                "`position_x` FLOAT NOT NULL, `position_y` FLOAT NOT NULL, `position_z` FLOAT NOT NULL, " +
                "`heading` FLOAT NOT NULL DEFAULT 0, `radius` FLOAT NOT NULL DEFAULT 2.5, " +
                "`prop_model` VARCHAR(100) NOT NULL DEFAULT '', `prop_height` FLOAT NOT NULL DEFAULT 0, `route` TEXT NULL, " +
                "`created_by` VARCHAR(50), `created_at` INT UNSIGNED NOT NULL DEFAULT 0, INDEX `idx_stat_type` (`stat_type`)) " +
                "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            foreach (var spalte in new[] { "`last_login` INT UNSIGNED NOT NULL DEFAULT 0", "`last_abbau` INT UNSIGNED NOT NULL DEFAULT 0",
                "`total_training_sessions` INT UNSIGNED NOT NULL DEFAULT 0" })
                await Sql("update", "ALTER TABLE `player_fitness` ADD COLUMN IF NOT EXISTS " + spalte);
            foreach (var spalte in new[] { "`prop_model` VARCHAR(100) NOT NULL DEFAULT ''", "`prop_height` FLOAT NOT NULL DEFAULT 0", "`route` TEXT NULL" })
                await Sql("update", "ALTER TABLE `fitness_stations` ADD COLUMN IF NOT EXISTS " + spalte);
            await Sql("update", "CREATE TABLE IF NOT EXISTS `fitness_gyms` (`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(60) NOT NULL, `created_at` INT UNSIGNED NOT NULL DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            await Sql("update", "ALTER TABLE `fitness_stations` ADD COLUMN IF NOT EXISTS `gym_id` INT UNSIGNED NULL DEFAULT NULL");
            await Sql("update", "ALTER TABLE `fitness_gyms` ADD COLUMN IF NOT EXISTS `job` VARCHAR(50) NOT NULL DEFAULT ''");
            await Sql("update", $"ALTER TABLE `fitness_gyms` ADD COLUMN IF NOT EXISTS `preis_tag` INT UNSIGNED NOT NULL DEFAULT {Config.PreisTag}");
            await Sql("update", $"ALTER TABLE `fitness_gyms` ADD COLUMN IF NOT EXISTS `preis_woche` INT UNSIGNED NOT NULL DEFAULT {Config.PreisWoche}");
            await Sql("update", $"ALTER TABLE `fitness_gyms` ADD COLUMN IF NOT EXISTS `preis_monat` INT UNSIGNED NOT NULL DEFAULT {Config.PreisMonat}");
            await Sql("update", "ALTER TABLE `fitness_gyms` ADD COLUMN IF NOT EXISTS `empfang` VARCHAR(80) NULL DEFAULT NULL");
            await Sql("update", "ALTER TABLE `fitness_gyms` ADD COLUMN IF NOT EXISTS `shop` VARCHAR(255) NOT NULL DEFAULT ''");
            foreach (var stat in Config.Stats)
            {
                await Sql("update", $"ALTER TABLE `player_fitness` ADD COLUMN IF NOT EXISTS `last_{stat}` INT UNSIGNED NOT NULL DEFAULT 0");
                await Sql("update", $"ALTER TABLE `player_fitness` ADD COLUMN IF NOT EXISTS `abbau_{stat}` INT UNSIGNED NOT NULL DEFAULT 0");
            }
            await GymsLaden(false);
            KarteRegistrieren();
            Tick += AblaufTick;
        }

        private async Task GymsLaden(bool senden)
        {
            var zeilen = await Abfrage("SELECT * FROM fitness_gyms ORDER BY name");
            gyms.Clear();
            foreach (var z in zeilen)
            {
                var g = new Gym
                {
                    Id = Zahl(z, "id"), Name = Text(z, "name"), Job = Text(z, "job"),
                    PreisTag = Zahl(z, "preis_tag", Config.PreisTag), PreisWoche = Zahl(z, "preis_woche", Config.PreisWoche), PreisMonat = Zahl(z, "preis_monat", Config.PreisMonat)
                };
                foreach (var teil in (Text(z, "shop") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = teil.Split('=');
                    int p;
                    if (kv.Length == 2 && int.TryParse(kv[1], out p)) g.Shop[kv[0]] = p;
                }
                var ex = Text(z, "empfang");
                if (!string.IsNullOrEmpty(ex))
                {
                    var teile = ex.Split(',');
                    float a, b, c;
                    if (teile.Length == 3 && float.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)
                        && float.TryParse(teile[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b)
                        && float.TryParse(teile[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c))
                        g.Empfang = new[] { a, b, c };
                }
                gyms.Add(g);
            }
            if (senden) GymsSenden(null);
        }

        private List<object> GymsFuerClient()
        {
            return gyms.Select(g => (object)new Dictionary<string, object>
            {
                { "id", g.Id }, { "name", g.Name }, { "job", g.Job ?? "" },
                { "tag", g.PreisTag }, { "woche", g.PreisWoche }, { "monat", g.PreisMonat },
                { "shop", Config.Supplements.ToDictionary(s => s.Item, s => (object)g.ShopPreis(s)) },
                { "empfang", g.Empfang != null ? (object)new List<object> { g.Empfang[0], g.Empfang[1], g.Empfang[2] } : null }
            }).ToList();
        }

        private void GymsSenden(Player player)
        {
            if (player == null) TriggerClientEvent("toter-fitness:client:gyms", GymsFuerClient());
            else TriggerClientEvent(player, "toter-fitness:client:gyms", GymsFuerClient());
        }

        private string GymName(int id)
        {
            var g = gyms.FirstOrDefault(x => x.Id == id);
            return g != null ? g.Name : null;
        }

        private void KarteRegistrieren()
        {
            ItemAnlegen(Config.KartenItem, "Gym-Mitgliedskarte", 0, true, "Mitgliedskarte für ein Fitnessstudio. Benutzen, um sie zu zeigen.");
            ItemBenutzbar(Config.KartenItem, KarteBenutzt);
        }

        private class Karte
        {
            public int Slot;
            public int GymId;
            public string Gym, Name, Geburtstag, CitizenId, Nummer;
            public long Ablauf;
        }

        private static IDictionary<string, object> AlsTabelle(object o)
        {
            return o as IDictionary<string, object>;
        }

        private List<Karte> KartenVon(Player player)
        {
            var liste = new List<Karte>();
            foreach (var item in ItemsVon(player))
            {
                if (Text(item, "name") != Config.KartenItem) continue;
                object infoRoh;
                item.TryGetValue("info", out infoRoh);
                var info = AlsTabelle(infoRoh);
                liste.Add(new Karte
                {
                    Slot = Zahl(item, "slot"),
                    GymId = Zahl(info, "gymId"),
                    Gym = Text(info, "gym"),
                    Name = Text(info, "name"),
                    Geburtstag = Text(info, "geburtstag"),
                    CitizenId = Text(info, "citizenid"),
                    Nummer = Text(info, "nummer"),
                    Ablauf = GrosseZahl(info, "ablauf")
                });
            }
            return liste;
        }

        private Karte GueltigeKarte(Player player, string cid, int gymId)
        {
            return KartenVon(player).Where(k => k.GymId == gymId && k.CitizenId == cid && k.Ablauf > Jetzt())
                .OrderByDescending(k => k.Ablauf).FirstOrDefault();
        }

        private bool HatMitgliedschaft(Player player, string cid)
        {
            return KartenVon(player).Any(k => k.CitizenId == cid && k.Ablauf > Jetzt());
        }

        private int AbbauRate(Player player, string cid)
        {
            return player != null && HatMitgliedschaft(player, cid) ? Config.AbbauMitMitgliedschaft : Config.AbbauOhneMitgliedschaft;
        }

        // Zeitzone des Servers für Tageswechsel, Kurszeiten und Anzeigen (Config.Zeitzone, unter Windows ZeitzoneWindows).
        private static TimeZoneInfo zeitzone;

        private static TimeZoneInfo Zeitzone()
        {
            if (zeitzone != null) return zeitzone;
            foreach (var id in new[] { Config.Zeitzone, Config.ZeitzoneWindows })
            {
                try { return zeitzone = TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Zeitzone {id} nicht gefunden: {e.Message}"); }
            }
            return zeitzone = TimeZoneInfo.Local;
        }

        private static DateTime Berlin(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, Zeitzone());
        }

        private static long TagesStart()
        {
            var utc = DateTime.UtcNow;
            var lokal = Berlin(utc);
            var start = lokal.Date.AddHours(Config.TagesResetStunde);
            if (lokal < start) start = start.AddDays(-1);
            var versatz = lokal - utc;
            return (long)(start - versatz - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private static string Datum(long unix)
        {
            var zeit = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
            zeit = TimeZoneInfo.ConvertTimeFromUtc(zeit, Zeitzone());
            return zeit.ToString("dd.MM.yyyy HH:mm", De);
        }

        private long naechsterAblauf;

        private async Task AblaufTick()
        {
            await Delay(1000);
            if (GetGameTimer() < naechsterAblauf) return;
            naechsterAblauf = GetGameTimer() + Config.KartenPruefenSekunden * 1000;
            try { BuchungenPruefen(); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Buchungen: {e.Message}"); }
            foreach (var player in Players.ToList())
            {
                try
                {
                    AbgelaufeneEntfernen(player);
                    var cid = CitizenId(player);
                    Werte w;
                    if (cid != null && werte.TryGetValue(cid, out w) && Abbauen(w, AbbauRate(player, cid)))
                    {
                        await Speichern(cid, w);
                        StatsSenden(player, w, false);
                    }
                }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Ablauf prüfen: {e.Message}"); }
            }
        }

        private void AbgelaufeneEntfernen(Player player)
        {
            var jetzt = Jetzt();
            foreach (var k in KartenVon(player).Where(k => k.Ablauf > 0 && k.Ablauf <= jetzt))
            {
                ItemNehmen(player, Config.KartenItem, 1, k.Slot);
                Melden(player, $"Deine Mitgliedskarte für {k.Gym} ist abgelaufen.", "error");
            }
        }

        private void KarteBenutzt(int quelle, object itemRoh)
        {
            try
            {
                var player = SpielerVon(quelle);
                var item = AlsTabelle(itemRoh);
                if (player == null || item == null) return;
                object infoRoh;
                item.TryGetValue("info", out infoRoh);
                var info = AlsTabelle(infoRoh);
                if (info == null) return;

                var ablauf = GrosseZahl(info, "ablauf");
                if (ablauf > 0 && ablauf <= Jetzt())
                {
                    AbgelaufeneEntfernen(player);
                    return;
                }
                var karte = new Dictionary<string, object>
                {
                    { "gym", Text(info, "gym") },
                    { "name", Text(info, "name") },
                    { "geburtstag", Text(info, "geburtstag") },
                    { "nummer", Text(info, "nummer") },
                    { "ablauf", Datum(ablauf) },
                    { "restTage", (int)Math.Max(0, (ablauf - Jetzt()) / 86400) },
                    { "zeiger", player.Name }
                };
                var pos = Position(player);
                foreach (var p in Players.ToList())
                {
                    var andere = Position(p);
                    if (p.Handle == player.Handle || Abstand(pos[0], pos[1], pos[2], andere[0], andere[1], andere[2]) < 3f)
                        TriggerClientEvent(p, "toter-fitness:client:karteZeigen", karte, p.Handle == player.Handle);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Karte zeigen: {e}");
            }
        }

        [EventHandler("toter-fitness:server:gymAktion")]
        private async void OnGymAktion([FromSource] Player player, string aktion, object datenRoh)
        {
            if (!IsPlayerAceAllowed(player.Handle, "command.fitnessplace"))
            {
                Debug.WriteLine($"[{Config.Ressource}] Gym-Aktion ohne Rechte von {player.Name}: {aktion}");
                return;
            }
            var d = AlsTabelle(datenRoh) ?? new Dictionary<string, object>();
            try
            {
                switch (aktion)
                {
                    case "neu":
                    {
                        var name = Text(d, "name").Trim();
                        if (name.Length < 2 || name.Length > 40) { Melden(player, "Der Gym-Name muss 2 bis 40 Zeichen haben.", "error"); return; }
                        await Sql("insert", "INSERT INTO fitness_gyms (name, created_at) VALUES (?, ?)", name, Jetzt());
                        await GymsLaden(true);
                        Melden(player, $"Gym \"{name}\" angelegt.", "success");
                        break;
                    }
                    case "umbenennen":
                    {
                        var id = Zahl(d, "id");
                        var name = Text(d, "name").Trim();
                        if (GymName(id) == null || name.Length < 2 || name.Length > 40) return;
                        await Sql("update", "UPDATE fitness_gyms SET name = ? WHERE id = ?", name, id);
                        await GymsLaden(true);
                        Melden(player, $"Gym heißt jetzt \"{name}\". Bereits ausgestellte Karten behalten den alten Namen.", "success");
                        break;
                    }
                    case "loeschen":
                    {
                        var id = Zahl(d, "id");
                        var name = GymName(id);
                        if (name == null) return;
                        await Sql("update", "UPDATE fitness_stations SET gym_id = NULL WHERE gym_id = ?", id);
                        await Sql("update", "DELETE FROM fitness_gyms WHERE id = ?", id);
                        foreach (var s in stationen.Where(s => s.GymId == id)) s.GymId = 0;
                        await GymsLaden(true);
                        TriggerClientEvent("toter-fitness:client:stationen", StationenFuerClient());
                        Melden(player, $"Gym \"{name}\" gelöscht. Seine Stationen kosten jetzt wieder pro Training.", "success");
                        break;
                    }
                    case "station":
                    {
                        var s = stationen.FirstOrDefault(x => x.Id == Zahl(d, "station"));
                        var gymId = Zahl(d, "gym");
                        if (s == null || s.Art != "studio" || (gymId != 0 && GymName(gymId) == null)) return;
                        await Sql("update", "UPDATE fitness_stations SET gym_id = ? WHERE id = ?", gymId == 0 ? (object)null : gymId, s.Id);
                        s.GymId = gymId;
                        TriggerClientEvent("toter-fitness:client:stationen", StationenFuerClient());
                        Melden(player, gymId == 0 ? $"Station #{s.Id} gehört zu keinem Gym mehr." : $"Station #{s.Id} gehört jetzt zu {GymName(gymId)}.", "success");
                        break;
                    }
                    case "karte":
                        KarteAusstellen(player, Zahl(d, "ziel"), Zahl(d, "gym"), GrosseZahl(d, "sekunden"));
                        break;
                    case "einstellungen":
                    {
                        var g = gyms.FirstOrDefault(x => x.Id == Zahl(d, "id"));
                        if (g == null) return;
                        var job = Text(d, "job").Trim().ToLowerInvariant();
                        if (job.Length > 50) return;
                        Func<string, int> preis = k => Math.Max(0, Math.Min(1000000, Zahl(d, k)));
                        object shopRoh;
                        d.TryGetValue("shop", out shopRoh);
                        var shopNeu = AlsTabelle(shopRoh);
                        var shop = string.Join(";", Config.Supplements.Select(s =>
                            s.Item + "=" + (shopNeu != null && shopNeu.ContainsKey(s.Item) ? Math.Max(0, Math.Min(1000000, Zahl(shopNeu, s.Item))) : g.ShopPreis(s))));
                        await Sql("update", "UPDATE fitness_gyms SET job = ?, preis_tag = ?, preis_woche = ?, preis_monat = ?, shop = ? WHERE id = ?",
                            job, preis("tag"), preis("woche"), preis("monat"), shop, g.Id);
                        await GymsLaden(true);
                        Melden(player, $"{g.Name} gespeichert" + (job == "" ? ", Verkauf am Empfang als Selbstbedienung." : $", verkaufen dürfen Mitarbeiter mit Job \"{job}\"."), "success");
                        break;
                    }
                    case "empfangSetzen":
                    case "empfangLoeschen":
                    {
                        var g = gyms.FirstOrDefault(x => x.Id == Zahl(d, "id"));
                        if (g == null) return;
                        string wert = null;
                        if (aktion == "empfangSetzen")
                        {
                            var p = Position(player);
                            wert = string.Join(",", new[] { p[0], p[1], p[2] }.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));
                        }
                        await Sql("update", "UPDATE fitness_gyms SET empfang = ? WHERE id = ?", wert, g.Id);
                        await GymsLaden(true);
                        Melden(player, wert != null ? $"Empfang von {g.Name} an deiner Position gesetzt." : $"Empfang von {g.Name} entfernt.", "success");
                        break;
                    }
                    case "platzieren":
                    {
                        var x = Kommazahl(d, "x"); var y = Kommazahl(d, "y"); var z = Kommazahl(d, "z");
                        var heading = Kommazahl(d, "heading");
                        var hoehe = Math.Max(-1f, Math.Min(2f, Kommazahl(d, "hoehe")));
                        var pos = Position(player);
                        if (Abstand(pos[0], pos[1], pos[2], x, y, z) > 20f) { Melden(player, "Zu weit weg zum Platzieren.", "error"); return; }
                        var stationId = Zahl(d, "station");
                        if (stationId > 0)
                        {
                            var s = stationen.FirstOrDefault(st => st.Id == stationId);
                            if (s == null) return;
                            await Sql("update", "UPDATE fitness_stations SET position_x = ?, position_y = ?, position_z = ?, heading = ?, prop_height = ? WHERE id = ?",
                                x, y, z, heading, hoehe, s.Id);
                            Melden(player, $"Station #{s.Id} versetzt.", "success");
                        }
                        else
                        {
                            var stat = Text(d, "stat");
                            var art = Text(d, "art");
                            var uebung = Config.UebungFuer(stat, art);
                            if (uebung == null) return;
                            var name = uebung.Name + (art == "studio" ? " (Studio)" : "");
                            var id = await Sql("insert",
                                "INSERT INTO fitness_stations (name, stat_type, station_type, position_x, position_y, position_z, heading, prop_model, prop_height, created_by, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                                name, stat, Config.ArtZuDb(art), x, y, z, heading, Text(d, "prop"), hoehe, CitizenId(player) ?? "", Jetzt());
                            Melden(player, $"Station #{id} gesetzt: {name}." + (uebung.Route ? " Strecke im Menü mit + Punkt ablaufen." : ""), "success");
                        }
                        StationenLaden();
                        break;
                    }
                    case "spieler":
                        await SpielerWerteSenden(player, Zahl(d, "ziel"));
                        break;
                    case "kurseAlle":
                        AdminKurseSenden(player);
                        break;
                    case "kursAbsagen":
                    {
                        var k = kurse.FirstOrDefault(x => x.Id == Zahl(d, "id"));
                        if (k == null) { AdminKurseSenden(player); return; }
                        await KursEntfernen(k);
                        foreach (var t in OnlineMit(k.Teilnehmer))
                            HandyNachricht(t, "Kurs abgesagt", $"„{k.Titel}“ am {KursZeit(k.Start)} findet nicht statt.");
                        var leiter = Players.ToList().FirstOrDefault(p => CitizenId(p) == k.LeiterCid);
                        if (leiter != null) HandyNachricht(leiter, "Kurs abgesagt", $"Ein Admin hat „{k.Titel}“ am {KursZeit(k.Start)} abgesagt.");
                        Melden(player, $"Kurs „{k.Titel}“ abgesagt.", "primary");
                        Debug.WriteLine($"[{Config.Ressource}] Admin {player.Name}: Kurs {k.Id} „{k.Titel}“ abgesagt");
                        AdminKurseSenden(player);
                        break;
                    }
                    case "besten":
                        await AdminBestenSenden(player, Text(d, "uebung"));
                        break;
                    case "bestenLoeschen":
                    case "bestenReset":
                    {
                        var uebung = Text(d, "uebung");
                        if (!Config.Uebungen.ContainsKey(uebung)) return;
                        if (aktion == "bestenReset")
                        {
                            await Sql("update", "DELETE FROM fitness_rekorde WHERE uebung = ?", uebung);
                            Melden(player, $"Bestenliste {Config.Uebungen[uebung].Name} zurückgesetzt.", "primary");
                        }
                        else
                        {
                            await Sql("update", "DELETE FROM fitness_rekorde WHERE uebung = ? AND citizenid = ?", uebung, Text(d, "cid"));
                            Melden(player, "Eintrag gelöscht.", "primary");
                        }
                        Debug.WriteLine($"[{Config.Ressource}] Admin {player.Name}: {aktion} {uebung} {Text(d, "cid")}");
                        await AdminBestenSenden(player, uebung);
                        break;
                    }
                    case "werte":
                    case "freigeben":
                    case "kater":
                    case "wirkung":
                    case "probetag":
                    {
                        var ziel = SpielerVon(Zahl(d, "ziel"));
                        var cid = ziel != null ? CitizenId(ziel) : null;
                        if (cid == null) { Melden(player, $"Spieler {Zahl(d, "ziel")} ist nicht online oder hat keinen Charakter geladen.", "error"); return; }
                        var w = await WerteHolen(cid, ziel);
                        if (aktion == "werte")
                        {
                            object neuRoh;
                            d.TryGetValue("werte", out neuRoh);
                            var neu = AlsTabelle(neuRoh);
                            if (neu == null) return;
                            foreach (var st in Config.Stats)
                                if (neu.ContainsKey(st)) w.Stat[st] = Begrenzen(Zahl(neu, st));
                            Melden(player, $"Werte von {ziel.Name} gespeichert.", "success");
                        }
                        else if (aktion == "wirkung")
                        {
                            var art = Text(d, "art");
                            if (art == "shake") w.ShakeBis = 0;
                            else if (art == "vitamine") w.VitamineBis = 0;
                            else return;
                            Melden(player, $"{(art == "shake" ? "Shake" : "Vitamine")} bei {ziel.Name} entfernt.", "success");
                            Debug.WriteLine($"[{Config.Ressource}] Admin {player.Name}: {art} entfernt bei {ziel.Name}");
                        }
                        else if (aktion == "probetag")
                        {
                            var gymId = Zahl(d, "gym");
                            await Sql("update", "DELETE FROM fitness_probetage WHERE citizenid = ? AND gym_id = ?", cid, gymId);
                            var gname = gyms.FirstOrDefault(x => x.Id == gymId)?.Name ?? $"Gym {gymId}";
                            Melden(player, $"Probetag bei {gname} für {ziel.Name} wieder frei.", "success");
                            Debug.WriteLine($"[{Config.Ressource}] Admin {player.Name}: Probetag {gname} frei für {ziel.Name}");
                        }
                        else if (aktion == "kater")
                        {
                            object anRoh;
                            var an = d.TryGetValue("an", out anRoh) && anRoh is bool ab && ab;
                            var jetzt = Jetzt();
                            w.MuskelkaterAb = an ? jetzt : 0;
                            w.MuskelkaterBis = an ? jetzt + Config.MuskelkaterStunden * 3600L : 0;
                            Melden(player, an ? $"{ziel.Name} hat jetzt Muskelkater bis {Datum(w.MuskelkaterBis).Substring(11)}." : $"Muskelkater von {ziel.Name} entfernt.", "success");
                            if (ziel.Handle != player.Handle)
                                Melden(ziel, an ? "Du hast Muskelkater und bist etwas langsamer." : "Dein Muskelkater ist weg.", an ? "primary" : "success");
                            Debug.WriteLine($"[{Config.Ressource}] Admin {player.Name}: Muskelkater {(an ? "an" : "aus")} für {ziel.Name}");
                        }
                        else
                        {
                            var stat = Text(d, "stat");
                            foreach (var st in Config.Stats)
                                if (stat == "alle" || stat == st) { w.Freigegeben.Add(st); w.Abklingen.Remove(st); }
                            Melden(player, stat == "alle"
                                ? $"{ziel.Name} darf heute alles noch einmal trainieren."
                                : $"{ziel.Name} darf {(Config.StatNamen.ContainsKey(stat) ? Config.StatNamen[stat] : stat)} heute noch einmal trainieren.", "success");
                            if (ziel.Handle != player.Handle) Melden(ziel, "Ein Admin hat dir heute ein weiteres Training freigegeben.", "success");
                        }
                        await Speichern(cid, w);
                        StatsSenden(ziel, w, false);
                        await SpielerWerteSenden(player, Zahl(d, "ziel"));
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Gym-Aktion {aktion}: {e}");
                Melden(player, "Das hat nicht geklappt, Details in der Server-Konsole.", "error");
            }
        }

        private void AdminKurseSenden(Player admin)
        {
            var liste = kurse.OrderBy(k => k.Start).Select(k => (object)new Dictionary<string, object>
            {
                { "id", k.Id }, { "titel", k.Titel }, { "gym", gyms.FirstOrDefault(g => g.Id == k.GymId)?.Name ?? "?" },
                { "zeit", KursZeit(k.Start) }, { "leiter", k.Leiter }, { "teilnehmer", k.Teilnehmer.Count }, { "max", k.Max },
                { "laeuft", k.Laeuft }, { "uebung", Config.Uebungen[k.Uebung].Name }
            }).ToList();
            TriggerClientEvent(admin, "toter-fitness:client:adminDaten", "kurse", liste);
        }

        private async Task AdminBestenSenden(Player admin, string uebung)
        {
            if (!Config.Uebungen.ContainsKey(uebung)) return;
            var zeilen = await Abfrage("SELECT citizenid, name, wert, datum FROM fitness_rekorde WHERE uebung = ? ORDER BY wert DESC, datum ASC LIMIT 50", uebung);
            TriggerClientEvent(admin, "toter-fitness:client:adminDaten", "besten", new Dictionary<string, object>
            {
                { "uebung", uebung }, { "einheit", Config.RekordEinheit(Config.Uebungen[uebung].Spiel) },
                { "liste", zeilen.Select(z => (object)new Dictionary<string, object>
                    {
                        { "cid", Text(z, "citizenid") }, { "name", Text(z, "name") }, { "wert", Zahl(z, "wert") },
                        { "datum", Datum(GrosseZahl(z, "datum")).Substring(0, 10) }
                    }).ToList() }
            });
        }

        private async Task SpielerWerteSenden(Player admin, int zielId)
        {
            var ziel = SpielerVon(zielId);
            var cid = ziel != null ? CitizenId(ziel) : null;
            if (cid == null)
            {
                TriggerClientEvent(admin, "toter-fitness:client:spieler", new Dictionary<string, object> { { "fehler", $"Spieler {zielId} ist nicht online." } });
                return;
            }
            var w = await WerteHolen(cid, ziel);
            var tag = TagesStart();
            var daten = new Dictionary<string, object>
            {
                { "id", zielId },
                { "name", ziel.Name },
                { "mitglied", HatMitgliedschaft(ziel, cid) },
                { "kater", HatMuskelkater(w) ? Datum(w.MuskelkaterBis).Substring(11) : w.MuskelkaterAb > Jetzt() ? "morgen" : "" },
                { "shake", w.ShakeBis > Jetzt() ? Datum(w.ShakeBis).Substring(11) : "" },
                { "vitamine", w.VitamineBis > Jetzt() ? Datum(w.VitamineBis).Substring(11) : "" },
                { "probetage", (await Abfrage("SELECT gym_id FROM fitness_probetage WHERE citizenid = ?", cid))
                    .Select(z => Zahl(z, "gym_id")).Select(id => (object)new Dictionary<string, object>
                        { { "id", id }, { "name", gyms.FirstOrDefault(g => g.Id == id)?.Name ?? $"Gym {id}" } }).ToList() }
            };
            var liste = new List<object>();
            foreach (var st in Config.Stats)
            {
                long letztes;
                var heute = w.LetztesStat.TryGetValue(st, out letztes) && letztes >= tag && !w.Freigegeben.Contains(st);
                liste.Add(new Dictionary<string, object>
                {
                    { "stat", st }, { "name", Config.StatNamen[st] }, { "wert", w.Stat[st] }, { "heute", heute },
                    { "zuletzt", letztes > 0 ? Datum(letztes) : "nie" }
                });
            }
            daten["werte"] = liste;
            TriggerClientEvent(admin, "toter-fitness:client:spieler", daten);
        }

        private bool KarteAusstellen(Player admin, int zielId, int gymId, long sekunden)
        {
            var ziel = SpielerVon(zielId);
            var gym = GymName(gymId);
            if (ziel == null) { Melden(admin, $"Spieler {zielId} ist nicht online.", "error"); return false; }
            if (gym == null) { Melden(admin, "Dieses Gym gibt es nicht.", "error"); return false; }
            sekunden = Math.Max(60, Math.Min(sekunden, 365L * 86400));

            var cid = CitizenId(ziel);
            if (cid == null) return false;
            var name = Charname(ziel);
            var geburtstag = Geburtstag(ziel);

            var alt = KartenVon(ziel).Where(k => k.GymId == gymId && k.CitizenId == cid).ToList();
            var start = Math.Max(Jetzt(), alt.Count > 0 ? alt.Max(k => k.Ablauf) : 0);
            var nummer = alt.Count > 0 && !string.IsNullOrEmpty(alt[0].Nummer) ? alt[0].Nummer : KartenNummer(gymId);
            foreach (var k in alt) ItemNehmen(ziel, Config.KartenItem, 1, k.Slot);

            var ablauf = start + sekunden;
            var info = new Dictionary<string, object>
            {
                { "gym", gym },
                { "gymId", gymId },
                { "name", name },
                { "geburtstag", geburtstag },
                { "citizenid", cid },
                { "nummer", nummer },
                { "ablauf", ablauf },
                { "gueltigBis", Datum(ablauf) }
            };
            if (!ItemGeben(ziel, Config.KartenItem, 1, info))
            {
                Melden(admin, $"{name} hat keinen Platz im Inventar.", "error");
                return false;
            }
            var verb = alt.Count > 0 ? "verlängert" : "ausgestellt";
            Melden(admin, $"Karte für {name} ({gym}) {verb}, gültig bis {Datum(ablauf)}.", "success");
            if (ziel.Handle != admin.Handle) Melden(ziel, $"Du hast eine Mitgliedskarte für {gym} bekommen, gültig bis {Datum(ablauf)}.", "success");
            return true;
        }

        private static float Kommazahl(IDictionary<string, object> d, string schluessel)
        {
            object o;
            if (d == null || !d.TryGetValue(schluessel, out o) || o == null) return 0f;
            try { return Convert.ToSingle(o, CultureInfo.InvariantCulture); }
            catch { return 0f; }
        }

        private string KartenNummer(int gymId)
        {
            return $"{gymId:00}-{zufall.Next(1000, 9999)}-{zufall.Next(1000, 9999)}";
        }
    }
}
