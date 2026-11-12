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
    public partial class Main : BaseScript
    {
        private readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        private bool statsOffen;

        public Main()
        {
            EventHandlers["toter-fitness:client:stationen"] += new Action<object>(OnStationen);
            EventHandlers["toter-fitness:client:stats"] += new Action<object, object>(OnStats);
            EventHandlers["toter-fitness:client:start"] += new Action<object, object, object>(OnStart);
            EventHandlers["toter-fitness:client:routeZeigen"] += new Action<object>(OnRouteZeigen);
            SpielerGeladenAnmelden(OnPlayerLoaded);
            EventHandlers["onResourceStop"] += new Action<string>(OnResourceStop);

            NuiCallback("close", NuiClose);
            NuiCallback("phase", NuiPhase);
            NuiCallback("ergebnis", NuiErgebnis);
            NuiCallback("treffer", d => NuiTreffer(d));


            EventHandlers["toter-fitness:client:ausgabe"] += new Action<string>(Ausgabe);
            NuiCallback("diagnose", d =>
            {
                object art;
                if (!istAdmin || !d.TryGetValue("art", out art)) return;
                var a = Convert.ToString(art);
                var zeilen = a == "stationen" ? Info() : a == "kleidung" ? KleidungInfo() : Check();
                foreach (var z in zeilen) Debug.WriteLine($"[{Config.Ressource}] {z}");
                Nui(new Dictionary<string, object> { { "action", "diagnose" }, { "zeilen", zeilen.Cast<object>().ToList() } });
            });
            MenueAnmelden();
            TelefonAnmelden();

            KoerperExportsAnmelden();

            Tick += Starten;
            Tick += SucheTick;
            Tick += ZeichnenTick;
            Tick += TrainingTick;
            Tick += SchutzTick;
            Tick += EffekteTick;
            Tick += EmpfangTick;
            Tick += CoachPromptTick;
            EmpfangAnmelden();
        }

        private void NuiCallback(string name, Action<IDictionary<string, object>> handler)
        {
            RegisterNuiCallbackType(name);
            EventHandlers["__cfx_nui:" + name] += new Action<IDictionary<string, object>, CallbackDelegate>((daten, cb) =>
            {
                try { handler(daten ?? new Dictionary<string, object>()); }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] NUI {name}: {e}"); }
                cb(new { ok = true });
            });
        }

        private void Nui(Dictionary<string, object> nachricht)
        {
            SendNuiMessage(Json.Schreiben(nachricht));
        }

        private async Task Starten()
        {
            Tick -= Starten;
            await Delay(1500);
            TriggerServerEvent("toter-fitness:server:stationenHolen");
            TriggerServerEvent("toter-fitness:server:statsHolen", false);
            TriggerServerEvent("toter-fitness:server:adminPruefen");
            Vorschlaege();
        }

        private void OnPlayerLoaded()
        {
            TriggerServerEvent("toter-fitness:server:statsHolen", false);
            TriggerServerEvent("toter-fitness:server:adminPruefen");
        }

        private List<string> Info()
        {
            var pos = GetEntityCoords(PlayerPedId(), true);
            StationC nah = null;
            var best = float.MaxValue;
            foreach (var s in stationen)
            {
                var d = Abstand(pos, s.X, s.Y, s.Z);
                if (d < best) { best = d; nah = s; }
            }
            var erzeugt = 0;
            foreach (var h in props.Values) if (h != 0) erzeugt++;
            var text = $"{stationen.Count} Stationen geladen, {erzeugt} Props erzeugt.";
            if (nah != null)
            {
                var modell = string.IsNullOrEmpty(nah.Prop) ? "ohne Prop" : nah.Prop + (new Model(nah.Prop).IsInCdImage ? " (Modell vorhanden)" : " (MODELL FEHLT)");
                int h;
                var status = props.TryGetValue(nah.Id, out h) ? (h != 0 ? "erzeugt" : "wird geladen oder fehlt") : "nicht erzeugt";
                text += $" Nächste: #{nah.Id} {nah.Name} in {best:0} m, {modell}, Prop {status}.";
            }
            return new List<string> { text };
        }

        // Rückmeldungen für Admins: im offenen Menü als Einblendung, sonst als normale Meldung.
        private void Ausgabe(string text)
        {
            if (menueOffen) Nui(new Dictionary<string, object> { { "action", "ausgabe" }, { "text", text } });
            else Hinweis(text, "primary", 6000);
        }

        private void Vorschlaege()
        {
            foreach (var befehl in new[] { "/fitness", "/fitnesscheck", "/fitnessphase", "/fitnessmodell", "/fitnessplace", "/fitnessliste", "/fitnessinfo", "/fitnessset", "/fitnessdelete", "/fitnessmove",
                "/fitnesshoehe", "/fitnessdrehen", "/fitnessroute", "/fitnessversatz", "/fitnesstest", "/fitnesskleidung" })
                TriggerEvent("chat:removeSuggestion", befehl);
        }

        private void Vorschlag(string befehl, string hilfe, params string[] parameter)
        {
            var liste = new List<object>();
            for (var i = 0; i + 1 < parameter.Length; i += 2)
                liste.Add(new Dictionary<string, object> { { "name", parameter[i] }, { "help", parameter[i + 1] } });
            TriggerEvent("chat:addSuggestion", befehl, hilfe, liste);
        }

        private void OnStats(object daten, object oeffnen)
        {
            var d = daten as IDictionary<string, object>;
            if (d == null) return;
            foreach (var stat in Config.Stats)
            {
                object w;
                if (d.TryGetValue(stat, out w)) stats[stat] = Convert.ToInt32(w);
            }
            Effekte(true);
            object e;
            if (d.TryGetValue("einheiten", out e)) einheiten = Convert.ToInt32(e);
            object h;
            heuteTrainiert = d.TryGetValue("heute", out h) && h is System.Collections.IEnumerable hl
                ? hl.Cast<object>().Select(x => Convert.ToString(x)).ToList() : new List<string>();
            object mk, mm, sh, vi, tl;
            muskelkaterBis = d.TryGetValue("muskelkater", out mk) ? Convert.ToString(mk) : "";
            muskelkaterMorgen = d.TryGetValue("muskelkaterMorgen", out mm) ? Convert.ToString(mm) : "";
            shakeBis = d.TryGetValue("shake", out sh) ? Convert.ToString(sh) : "";
            vitamineBis = d.TryGetValue("vitamine", out vi) ? Convert.ToString(vi) : "";
            trainerDaten = d.TryGetValue("trainerDaten", out tl) ? tl : null;
            object ch;
            if (d.TryGetValue("chef", out ch)) chefDaten = ch;
            if (muskelkaterBis != "" && !muskelkaterGemeldet)
            {
                muskelkaterGemeldet = true;
                Hinweis($"Du hast Muskelkater und bist etwas langsamer (bis {muskelkaterBis.Substring(11)} Uhr). Muskelbalsam hilft.", "error", 6000);
            }
            if (muskelkaterBis == "") muskelkaterGemeldet = false;
            object k;
            meineKarten = d.TryGetValue("karten", out k) && k is System.Collections.IEnumerable liste
                ? liste.Cast<object>().ToList() : new List<object>();

            if (oeffnen is bool b && b) MenueSenden(true);
            else MenueSenden(false);
            AppSenden(d);
        }

        private int Wert(string stat)
        {
            int w;
            return stats.TryGetValue(stat, out w) ? w : Config.StatStart;
        }

        private void NuiClose(IDictionary<string, object> daten)
        {
            SetNuiFocus(false, false);
            statsOffen = false;
            menueOffen = false;
            if (beschaeftigt) TrainingAbbrechen();
        }

        private void OnResourceStop(string ressource)
        {
            if (ressource != GetCurrentResourceName()) return;
            if (statsOffen || beschaeftigt) SetNuiFocus(false, false);
            if (beschaeftigt) { ClearPedTasks(PlayerPedId()); SzenarioPropsEntfernen(PlayerPedId()); SteuerungFreigeben(PlayerPedId()); }
            if (platzieren) PlatzierenBeenden();
            AppEntfernen();
            Schutz(PlayerPedId(), false);
            AllePropsEntfernen();
        }
    }
}
