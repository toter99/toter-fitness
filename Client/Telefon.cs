using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    // Zwei Apps fürs Handy, Oberflächen in html/telefon:
    //   "Fitness" (fitness.html) für alle: Werte, Zustand, Mitgliedschaften, Stationen, Bestenliste
    //   "GymDesk" (gymdesk.html) nur für Mitarbeiter eines Gyms: Personal Training, Preise, Team
    // Die Seiten holen ihre Daten selbst über NUI-Callbacks dieser Ressource (app..., desk...).
    // lb-phone: Apps werden als Custom Apps angemeldet, GymDesk nur solange man im Gym arbeitet.
    public partial class Main
    {
        private readonly HashSet<string> angemeldeteApps = new HashSet<string>();
        private int appStand, trainerStand, bestenStand;
        private object letzteBestenliste;
        private object kurseDaten, letzterVerlauf;
        private int kursStand, verlaufStand;
        private IDictionary<string, object> studio;

        private void TelefonAnmelden()
        {
            EventHandlers["onClientResourceStart"] += new Action<string>(name =>
            {
                if (name != Config.Telefon) return;
                angemeldeteApps.Clear();
                AppsAbgleichen();
            });
            EventHandlers["toter-fitness:client:trainerListe"] += new Action<object>(d => trainerStand++);
            EventHandlers["toter-fitness:client:bestenliste"] += new Action<object>(d =>
            {
                letzteBestenliste = d;
                bestenStand++;
            });
            EventHandlers["toter-fitness:client:kurse"] += new Action<object>(d =>
            {
                kurseDaten = d;
                kursStand++;
            });
            EventHandlers["toter-fitness:client:verlauf"] += new Action<string, object>((stat, punkte) =>
            {
                letzterVerlauf = new Dictionary<string, object> { { "stat", stat }, { "punkte", punkte }, { "tage", Config.VerlaufTage } };
                verlaufStand++;
            });
            AppCallback("appVerlauf", async d =>
            {
                object stat;
                if (!d.TryGetValue("stat", out stat)) return null;
                var vorher = verlaufStand;
                TriggerServerEvent("toter-fitness:server:verlauf", Convert.ToString(stat));
                await Warten(() => verlaufStand != vorher);
                return letzterVerlauf;
            });
            AppCallback("appKurs", async d =>
            {
                object aktion, id;
                d.TryGetValue("aktion", out aktion);
                d.TryGetValue("id", out id);
                var a = Convert.ToString(aktion);
                var nummer = id != null ? Convert.ToInt32(id) : 0;
                if (a == "route")
                {
                    var k = (kurseDaten as System.Collections.IEnumerable)?.Cast<object>().OfType<IDictionary<string, object>>()
                        .FirstOrDefault(x => Convert.ToInt32(x["id"]) == nummer);
                    if (k != null) SetNewWaypoint(Convert.ToSingle(k["x"]), Convert.ToSingle(k["y"]));
                    return new Dictionary<string, object> { { "kurse", kurseDaten } };
                }
                var vorher = kursStand;
                if (a == "anmelden" || a == "abmelden") TriggerServerEvent("toter-fitness:server:kursAnmelden", nummer, a == "anmelden");
                else TriggerServerEvent("toter-fitness:server:kurse");
                await Warten(() => kursStand != vorher);
                return new Dictionary<string, object> { { "kurse", kurseDaten } };
            });
            AppCallback("deskKurs", async d =>
            {
                object aktion, id;
                d.TryGetValue("aktion", out aktion);
                d.TryGetValue("id", out id);
                var a = Convert.ToString(aktion);
                var vorher = kursStand;
                if (a == "anlegen")
                {
                    var neu = new Dictionary<string, object>(d);
                    object r;
                    neu.TryGetValue("route", out r);
                    var rid = r == null ? 0 : Convert.ToInt32(r);
                    var start = stationen.FirstOrDefault(s => s.Id == rid && s.Route.Count > 0);
                    var pos = GetEntityCoords(PlayerPedId(), true);
                    object u;
                    neu.TryGetValue("uebung", out u);
                    neu["ort"] = Convert.ToString(u) == "ausdauer:kurs-joggen" && start != null ? OrtVon(start.X, start.Y, start.Z) : OrtVon(pos.X, pos.Y, pos.Z);
                    TriggerServerEvent("toter-fitness:server:kursAnlegen", neu);
                }
                else if (a == "aufbauen" && id != null && KursVon(Convert.ToInt32(id)) is IDictionary<string, object> kurs
                         && !(kurs.TryGetValue("joggen", out var jog) && jog is bool jb && jb))
                {
                    MattenLegenStarten(kurs);
                    return DeskDaten();
                }
                else if (a == "absagen" && id != null) TriggerServerEvent("toter-fitness:server:kursAbsagen", Convert.ToInt32(id));
                else if ((a == "aufbauen" || a == "abbauen") && id != null) TriggerServerEvent("toter-fitness:server:kursAufbauen", Convert.ToInt32(id), a == "aufbauen");
                else if (a == "starten" && id != null)
                {
                    KursStartenNachHandy(Convert.ToInt32(id));
                    return DeskDaten();
                }
                else TriggerServerEvent("toter-fitness:server:kurse");
                await Warten(() => kursStand != vorher);
                return DeskDaten();
            });
            JobWechselAnmelden(async () =>
            {
                await Delay(500);
                TriggerServerEvent("toter-fitness:server:statsHolen", false);
            });

            AppCallback("appDaten", async d =>
            {
                await StatsAbwarten();
                await KurseFrisch();
                return AppDaten();
            });
            AppCallback("appWegpunkt", d =>
            {
                NuiWegpunkt(d);
                return Task.FromResult<object>(new Dictionary<string, object> { { "ok", true } });
            });
            AppCallback("appBestenliste", async d =>
            {
                object u;
                if (!d.TryGetValue("uebung", out u)) return null;
                var vorher = bestenStand;
                TriggerServerEvent("toter-fitness:server:bestenliste", Convert.ToString(u));
                await Warten(() => bestenStand != vorher);
                return letzteBestenliste;
            });
            AppCallback("deskDaten", async d =>
            {
                await StatsAbwarten();
                await KurseFrisch();
                return DeskDaten();
            });
            AppCallback("deskTraining", async d =>
            {
                object aktion, id, betrag;
                d.TryGetValue("aktion", out aktion);
                d.TryGetValue("id", out id);
                d.TryGetValue("betrag", out betrag);
                var vorher = trainerStand;
                var a = Convert.ToString(aktion);
                if ((a == "abrechnen" || a == "verwerfen") && id != null)
                    TriggerServerEvent("toter-fitness:server:trainingAbschliessen", Convert.ToInt32(id), a == "abrechnen" ? Convert.ToInt32(betrag ?? 0) : 0);
                else TriggerServerEvent("toter-fitness:server:trainerListe");
                await Warten(() => trainerStand != vorher, 2000);
                return DeskDaten();
            });
            AppCallback("deskPreise", async d =>
            {
                var vorher = appStand;
                TriggerServerEvent("toter-fitness:server:chefAktion", "preise", d);
                await Warten(() => appStand != vorher);
                return DeskDaten();
            });

            AppsAbgleichen();
        }

        private void AppCallback(string name, Func<IDictionary<string, object>, Task<object>> handler)
        {
            RegisterNuiCallbackType(name);
            EventHandlers["__cfx_nui:" + name] += new Action<IDictionary<string, object>, CallbackDelegate>(async (daten, cb) =>
            {
                object antwort = null;
                try { antwort = await handler(daten ?? new Dictionary<string, object>()); }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] App {name}: {e.Message}"); }
                cb(Normal(antwort ?? new Dictionary<string, object> { { "ok", false } }));
            });
        }

        // Erst das Handy weg, sonst liegt es über dem Minispiel. Dann startet der Server alle gleichzeitig.
        private async void KursStartenNachHandy(int id)
        {
            HandyZu();
            var bis = GetGameTimer() + 15000;
            while (IsNuiFocused() && GetGameTimer() < bis) await Delay(100);
            await Delay(300);
            TriggerServerEvent("toter-fitness:server:kursStarten", id);
        }

        // Beim Öffnen der App die Kurse frisch vom Server holen, sonst fehlen Kurse, die andere inzwischen geplant haben.
        private async Task KurseFrisch()
        {
            var vorher = kursStand;
            TriggerServerEvent("toter-fitness:server:kurse");
            await Warten(() => kursStand != vorher, 1500);
        }

        private static async Task Warten(Func<bool> fertig, int ms = 3000)
        {
            var ende = GetGameTimer() + ms;
            while (!fertig() && GetGameTimer() < ende) await Delay(50);
        }

        private async Task StatsAbwarten()
        {
            var vorher = appStand;
            TriggerServerEvent("toter-fitness:server:statsHolen", "app");
            await Warten(() => appStand != vorher);
        }

        // ---------------------------------------------------------------- lb-phone

        private void AppsAbgleichen()
        {
            if (GetResourceState(Config.Telefon) != "started") return;
            AppSetzen(Config.AppId, true, "Fitness", "Deine Werte, dein Training und deine Mitgliedschaften.", "fitness");
            AppSetzen(Config.DeskAppId, studio != null, "GymDesk", "Für das Team im Fitnessstudio: Personal Training, Preise, Team.", "gymdesk");
        }

        private void AppSetzen(string id, bool sichtbar, string name, string text, string seite)
        {
            try
            {
                if (sichtbar && !angemeldeteApps.Contains(id))
                {
                    var res = GetCurrentResourceName();
                    Exports[Config.Telefon].AddCustomApp(new Dictionary<string, object>
                    {
                        { "identifier", id }, { "name", name }, { "description", text }, { "developer", "toter" },
                        { "defaultApp", true }, { "size", 18000 },
                        { "ui", $"{res}/html/telefon/{seite}.html" },
                        { "icon", $"https://cfx-nui-{res}/html/telefon/{seite}.png" },
                        { "fixBlur", false }
                    });
                    angemeldeteApps.Add(id);
                }
                else if (!sichtbar && angemeldeteApps.Contains(id))
                {
                    Exports[Config.Telefon].RemoveCustomApp(id);
                    angemeldeteApps.Remove(id);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] App {name} im Handy: {e.Message}");
            }
        }

        private void AppEntfernen()
        {
            if (GetResourceState(Config.Telefon) != "started") return;
            foreach (var id in angemeldeteApps.ToList())
            {
                try { Exports[Config.Telefon].RemoveCustomApp(id); }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] App entfernen: {e.Message}"); }
            }
            angemeldeteApps.Clear();
        }

        // Nach jedem Eingang neuer Werte vom Server (OnStats).
        private void AppSenden(IDictionary<string, object> stats)
        {
            appStand++;
            object s;
            studio = stats != null && stats.TryGetValue("studio", out s) ? s as IDictionary<string, object> : null;
            object k;
            if (stats != null && stats.TryGetValue("kurse", out k)) kurseDaten = k;
            AppsAbgleichen();
            if (!angemeldeteApps.Contains(Config.AppId)) return;
            try { Exports[Config.Telefon].SendCustomAppMessage(Config.AppId, Normal(AppDaten())); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] App-Daten: {e.Message}"); }
        }

        // ---------------------------------------------------------------- Daten

        private Dictionary<string, object> AppDaten()
        {
            var werteListe = new Dictionary<string, object>();
            foreach (var stat in Config.Stats) werteListe[stat] = Wert(stat);
            var pos = GetEntityCoords(PlayerPedId(), true);
            return new Dictionary<string, object>
            {
                { "type", "fitness" },
                { "stats", werteListe },
                { "heute", heuteTrainiert.Cast<object>().ToList() },
                { "einheiten", einheiten },
                { "koerper", MeinKoerper() },
                { "muskelkater", muskelkaterBis }, { "muskelkaterMorgen", muskelkaterMorgen },
                { "shake", shakeBis }, { "vitamine", vitamineBis },
                { "karten", meineKarten },
                { "reset", Config.TagesResetStunde },
                { "negativUnter", Config.NegativUnter }, { "positivAb", Config.PositivAb }, { "vollAb", Config.VollAb },
                { "preis", Config.PreisStudio },
                { "kurse", kurseDaten },
                { "stationen", stationen.Where(s => s.KursId == 0).Select(s => new { s, d = Abstand(pos, s.X, s.Y, s.Z) }).OrderBy(x => x.d).Take(8).Select(x =>
                {
                    var u = Config.UebungFuer(x.s.Stat, x.s.Art);
                    return (object)new Dictionary<string, object>
                    {
                        { "id", x.s.Id }, { "name", u != null ? u.Name : x.s.Name }, { "stat", x.s.Stat },
                        { "studio", x.s.Art == "studio" }, { "gym", GymName(x.s.GymId) ?? "" }, { "abstand", (int)Math.Round(x.d) }
                    };
                }).ToList() },
                { "uebungen", Config.Uebungen.Where(kv => !Config.IstKurs(kv.Value.Art)).Select(kv => (object)new Dictionary<string, object>
                    { { "id", kv.Key }, { "name", kv.Value.Name + (kv.Value.Art == "studio" ? " (Studio)" : "") }, { "stat", kv.Value.Stat } }).ToList() }
            };
        }

        private Dictionary<string, object> DeskDaten()
        {
            return new Dictionary<string, object>
            {
                { "studio", studio },
                { "trainerDaten", trainerDaten },
                { "chef", chefDaten },
                { "minuten", Config.TrainerBuchungMinuten },
                { "kurse", kurseDaten },
                { "kursArten", Config.KursArten.Select(k => (object)new Dictionary<string, object>
                    { { "id", k }, { "name", Config.Uebungen[k].Name }, { "joggen", Config.Uebungen[k].Route } }).ToList() },
                { "maxTeilnehmer", Config.KursMaxTeilnehmer }, { "maxPreis", Config.TrainerMaxPreis },
                { "routen", RoutenInDerNaehe() }
            };
        }

        private IDictionary<string, object> KursVon(int id)
        {
            return (kurseDaten as System.Collections.IEnumerable)?.Cast<object>().OfType<IDictionary<string, object>>()
                .FirstOrDefault(k => k.TryGetValue("id", out var i) && Convert.ToInt32(i) == id);
        }

        // Straße und Gegend am Punkt, z. B. "Bay City Ave, Vespucci Beach".
        private static string OrtVon(float x, float y, float z)
        {
            uint strasse = 0, kreuzung = 0;
            GetStreetNameAtCoord(x, y, z, ref strasse, ref kreuzung);
            var name = strasse != 0 ? GetStreetNameFromHashKey(strasse) : "";
            var gegend = GetLabelText(GetNameOfZone(x, y, z));
            if (gegend == "NULL") gegend = "";
            return string.Join(", ", new[] { name, gegend }.Where(t => !string.IsNullOrEmpty(t)));
        }

        private static string Strecke(StationC s)
        {
            var meter = 0f;
            float lx = s.X, ly = s.Y, lz = s.Z;
            foreach (var p in s.Route)
            {
                meter += (float)Math.Sqrt((p[0] - lx) * (p[0] - lx) + (p[1] - ly) * (p[1] - ly) + (p[2] - lz) * (p[2] - lz));
                lx = p[0]; ly = p[1]; lz = p[2];
            }
            return meter >= 1000f ? $"{meter / 1000f:0.0} km".Replace('.', ',') : $"{(int)Math.Round(meter / 10f) * 10} m";
        }

        private List<object> RoutenInDerNaehe()
        {
            var pos = GetEntityCoords(PlayerPedId(), true);
            return stationen.Where(s => s.KursId == 0 && s.Art == "frei" && s.Route.Count > 0)
                .Select(s => new { s, d = Abstand(pos, s.X, s.Y, s.Z) }).OrderBy(x => x.d).Take(10)
                .Select(x => (object)new Dictionary<string, object>
                    { { "id", x.s.Id }, { "name", $"{Strecke(x.s)} · {(OrtVon(x.s.X, x.s.Y, x.s.Z) is string o && o != "" ? o : "Strecke " + x.s.Id)}" } }).ToList();
        }

        private static object Normal(object wert)
        {
            if (wert == null || wert is string) return wert;
            if (wert is IDictionary<string, object> d) return d.ToDictionary(kv => kv.Key, kv => Normal(kv.Value));
            if (wert is System.Collections.IEnumerable e) return e.Cast<object>().Select(Normal).ToList();
            return wert;
        }
    }
}
