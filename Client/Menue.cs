using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public partial class Main
    {
        private bool istAdmin;
        private List<object> meineKarten = new List<object>();
        private object chefDaten;
        private List<string> heuteTrainiert = new List<string>();
        private string muskelkaterBis = "", muskelkaterMorgen = "", shakeBis = "", vitamineBis = "";
        private bool muskelkaterGemeldet;
        private object trainerDaten;
        private readonly Dictionary<int, string> gyms = new Dictionary<int, string>();
        private readonly Dictionary<int, IDictionary<string, object>> gymDaten = new Dictionary<int, IDictionary<string, object>>();
        private readonly Dictionary<int, Vector3> empfaenge = new Dictionary<int, Vector3>();

        private object GymWert(int id, string schluessel, object standard)
        {
            IDictionary<string, object> g;
            object w;
            return gymDaten.TryGetValue(id, out g) && g.TryGetValue(schluessel, out w) && w != null ? w : standard;
        }

        private string GymName(int id)
        {
            string n;
            return id > 0 && gyms.TryGetValue(id, out n) ? n : null;
        }

        private void OnGyms(object daten)
        {
            gyms.Clear();
            gymDaten.Clear();
            empfaenge.Clear();
            if (daten is System.Collections.IEnumerable liste)
                foreach (var e in liste)
                    if (e is IDictionary<string, object> g)
                    {
                        var id = Convert.ToInt32(g["id"]);
                        gyms[id] = Convert.ToString(g["name"]);
                        gymDaten[id] = g;
                        object ex;
                        if (g.TryGetValue("empfang", out ex) && ex is System.Collections.IList p && p.Count == 3)
                            empfaenge[id] = new Vector3(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]));
                    }
            MenueSenden(false);
        }

        private void OnKarteZeigen(object karte, bool eigene)
        {
            var d = karte as IDictionary<string, object>;
            if (d == null) return;
            var nachricht = new Dictionary<string, object>(d) { { "action", "karte" }, { "eigene", eigene } };
            Nui(nachricht);
        }

        private async void NuiGymAktion(IDictionary<string, object> daten)
        {
            if (!istAdmin) return;
            object aktion, werte;
            daten.TryGetValue("aktion", out aktion);
            daten.TryGetValue("daten", out werte);
            TriggerServerEvent("toter-fitness:server:gymAktion", Convert.ToString(aktion), werte ?? new Dictionary<string, object>());
            await Delay(900);
            MenueSenden(false);
        }
        private bool menueOffen;
        private int einheiten;

        private static readonly HashSet<string> MenueBefehle = new HashSet<string>
        {
            "fitnessplace", "fitnessdelete", "fitnessmove", "fitnesshoehe", "fitnessdrehen", "fitnessroute", "fitnessset"
        };

        private static bool BefehlErlaubt(string befehl)
        {
            var teile = befehl.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (teile.Length == 0 || teile.Length > 4 || !MenueBefehle.Contains(teile[0].ToLowerInvariant())) return false;
            foreach (var t in teile.Skip(1))
                foreach (var c in t)
                    if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-')) return false;
            return true;
        }

        private void MenueAnmelden()
        {
            EventHandlers["toter-fitness:client:admin"] += new Action<bool>(OnAdmin);
            RegisterCommand("fitnessadmin", new Action<int, List<object>, string>((q, a, r) => { if (NurAdmin()) MenueOeffnen(true); }), false);
            NuiCallback("menueBefehl", NuiMenueBefehl);
            NuiCallback("wegpunkt", NuiWegpunkt);
            NuiCallback("gymAktion", NuiGymAktion);
            EventHandlers["toter-fitness:client:coachen"] += new Action<int, bool, bool>(Coachen);
            EventHandlers["toter-fitness:client:trainerListe"] += new Action<object>(daten => trainerDaten = daten);
            EventHandlers["toter-fitness:client:einnehmen"] += new Action<string>(Einnehmen);
            NuiCallback("platzieren", d =>
            {
                object stat, art, prop, station;
                d.TryGetValue("stat", out stat);
                d.TryGetValue("art", out art);
                d.TryGetValue("prop", out prop);
                d.TryGetValue("station", out station);
                PlatzierenStarten(Convert.ToString(stat), Convert.ToString(art), Convert.ToString(prop), station != null ? Convert.ToInt32(station) : 0);
            });
            NuiCallback("propVorschau", d => { object m; if (istAdmin && d.TryGetValue("modell", out m)) PropVorschau(Convert.ToString(m)); });
            EventHandlers["toter-fitness:client:gyms"] += new Action<object>(OnGyms);
            EventHandlers["toter-fitness:client:spieler"] += new Action<object>(daten =>
            {
                var d = daten as IDictionary<string, object>;
                if (d != null && istAdmin) Nui(new Dictionary<string, object>(d) { { "action", "spielerWerte" } });
            });
            EventHandlers["toter-fitness:client:karteZeigen"] += new Action<object, bool>(OnKarteZeigen);
            EventHandlers["toter-fitness:client:adminDaten"] += new Action<string, object>((art, daten) =>
            {
                if (istAdmin) Nui(new Dictionary<string, object> { { "action", "adminDaten" }, { "art", art }, { "daten", daten } });
            });
            NuiCallback("teleport", d =>
            {
                object id;
                if (!istAdmin || !d.TryGetValue("id", out id)) return;
                var s = stationen.FirstOrDefault(x => x.Id == Convert.ToInt32(id));
                if (s == null) return;
                SetPedCoordsKeepVehicle(PlayerPedId(), s.X, s.Y + 1.5f, s.Z);
            });
        }

        private void OnAdmin(bool admin)
        {
            var vorher = istAdmin;
            istAdmin = admin;
            if (admin && !vorher) Vorschlag("/fitnessadmin", "Fitness-Admin-Menü öffnen");
        }

        // Das Menü im Spiel ist nur noch das Admin-Menü (/fitnessadmin). Spieler nutzen die Handy-Apps.
        private void MenueOeffnen(bool admin = true)
        {
            if (beschaeftigt || !istAdmin) return;
            TriggerServerEvent("toter-fitness:server:statsHolen", true);
        }

        private void MenueSenden(bool oeffnen)
        {
            if (!oeffnen && !menueOffen) return;
            var pos = GetEntityCoords(PlayerPedId(), true);

            var naheListe = stationen
                .Where(s => s.KursId == 0)
                .Select(s => new { s, d = Abstand(pos, s.X, s.Y, s.Z) })
                .OrderBy(x => x.d)
                .Take(25)
                .Select(x =>
                {
                    var u = Config.UebungFuer(x.s.Stat, x.s.Art);
                    return (object)new Dictionary<string, object>
                    {
                        { "id", x.s.Id },
                        { "name", u != null ? u.Name : x.s.Name },
                        { "stat", x.s.Stat },
                        { "studio", x.s.Art == "studio" },
                        { "abstand", (int)Math.Round(x.d) },
                        { "route", x.s.Route.Count },
                        { "joggen", u != null && u.Route },
                        { "prop", x.s.Prop ?? "" },
                        { "gymId", x.s.GymId },
                        { "gym", GymName(x.s.GymId) ?? "" },
                        { "hoehe", Math.Round(x.s.Hoehe, 2) }
                    };
                }).ToList();

            var daten = new Dictionary<string, object>
            {
                { "action", "menue" },
                { "oeffnen", oeffnen },
                { "admin", istAdmin },
                { "stationen", naheListe },
                { "preis", Config.PreisStudio },
                { "meineId", GetPlayerServerId(PlayerId()) },
                { "uebungenAlle", Config.Uebungen.Where(kv => !Config.IstKurs(kv.Value.Art)).Select(kv => (object)new Dictionary<string, object>
                    { { "id", kv.Key }, { "name", kv.Value.Name + (kv.Value.Art == "studio" ? " (Studio)" : "") }, { "stat", kv.Value.Stat } }).ToList() },
                { "uebungen", Config.Uebungen.Values.Select(u => (object)new Dictionary<string, object>
                    { { "stat", u.Stat }, { "art", u.Art }, { "name", u.Name }, { "prop", u.Prop ?? "" } }).ToList() },
                { "gyms", gyms.OrderBy(g => g.Value).Select(g => (object)new Dictionary<string, object>
                {
                    { "id", g.Key }, { "name", g.Value }, { "stationen", stationen.Count(s => s.GymId == g.Key) },
                    { "job", GymWert(g.Key, "job", "") }, { "tag", GymWert(g.Key, "tag", 0) }, { "woche", GymWert(g.Key, "woche", 0) },
                    { "monat", GymWert(g.Key, "monat", 0) }, { "empfang", empfaenge.ContainsKey(g.Key) },
                    { "shop", GymWert(g.Key, "shop", new Dictionary<string, object>()) }
                }).ToList() },
                { "supplements", Config.Supplements.Select(s => (object)new Dictionary<string, object> { { "item", s.Item }, { "name", s.Name } }).ToList() }
            };
            Nui(daten);

            if (oeffnen)
            {
                SetNuiFocus(true, true);
                statsOffen = true;
                menueOffen = true;
            }
        }

        private async void NuiMenueBefehl(IDictionary<string, object> daten)
        {
            object roh;
            var befehl = daten.TryGetValue("befehl", out roh) ? Convert.ToString(roh).Trim() : "";
            if (!istAdmin || !BefehlErlaubt(befehl))
            {
                Debug.WriteLine($"[{Config.Ressource}] Menü-Befehl abgelehnt: {befehl}");
                return;
            }
            var teile = befehl.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            TriggerServerEvent("toter-fitness:server:adminBefehl", teile[0].ToLowerInvariant(), teile.Skip(1).Cast<object>().ToList());
            await Delay(700);
            if (befehl.StartsWith("fitnessset", StringComparison.OrdinalIgnoreCase))
                TriggerServerEvent("toter-fitness:server:statsHolen", false);
            await Delay(300);
            MenueSenden(false);
        }

        private void NuiWegpunkt(IDictionary<string, object> daten)
        {
            object roh;
            if (!daten.TryGetValue("id", out roh)) return;
            var id = Convert.ToInt32(roh);
            var s = stationen.FirstOrDefault(x => x.Id == id);
            if (s == null) return;
            SetNewWaypoint(s.X, s.Y);
            Hinweis("Wegpunkt zur Station gesetzt.", "success", 2500);
        }

        private int vorschauObjekt;

        private async void PropVorschau(string modell)
        {
            modell = (modell ?? "").Trim();
            if (modell == "" || modell == "-") return;
            if (vorschauObjekt != 0 && DoesEntityExist(vorschauObjekt)) DeleteObject(ref vorschauObjekt);
            var m = new Model(modell);
            if (!m.IsInCdImage)
            {
                Hinweis($"Das Modell {modell} gibt es nicht.", "error", 3500);
                return;
            }
            if (!await m.Request(5000)) return;
            var ped = PlayerPedId();
            var pos = GetOffsetFromEntityInWorldCoords(ped, 0f, 2.2f, -0.98f);
            var obj = Function.Call<int>(Hash.CREATE_OBJECT, m.Hash, pos.X, pos.Y, pos.Z, false, false, false);
            m.MarkAsNoLongerNeeded();
            if (obj == 0) return;
            SetEntityHeading(obj, GetEntityHeading(ped) + 180f);
            PlaceObjectOnGroundProperly(obj);
            FreezeEntityPosition(obj, true);
            vorschauObjekt = obj;
            Hinweis($"Vorschau: {modell} (20 Sekunden, nur für dich sichtbar)", "success", 3500);
            await Delay(20000);
            if (vorschauObjekt == obj && DoesEntityExist(obj)) DeleteObject(ref vorschauObjekt);
        }

        private async void Einnehmen(string art)
        {
            if (beschaeftigt) return;
            string dict, clip;
            switch (art)
            {
                case "trinken": dict = "mp_player_intdrink"; clip = "loop_bottle"; break;
                case "essen": dict = "mp_player_inteat@burger"; clip = "mp_player_int_eat_burger"; break;
                default: dict = "missmic4"; clip = "michael_tux_fidget"; break;
            }
            try
            {
                RequestAnimDict(dict);
                var bis = GetGameTimer() + 1500;
                while (!HasAnimDictLoaded(dict) && GetGameTimer() < bis) await Delay(20);
                if (HasAnimDictLoaded(dict)) TaskPlayAnim(PlayerPedId(), dict, clip, 4f, -4f, 2500, 49, 0f, false, false, false);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Einnehmen: {e.Message}");
            }
        }

        private bool NurAdmin()
        {
            if (istAdmin) return true;
            Hinweis("Das dürfen nur Admins.", "error", 2500);
            return false;
        }

    }
}
