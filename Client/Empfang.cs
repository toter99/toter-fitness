using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public partial class Main
    {
        private int empfangNaechste;
        private int empfangNah;

        private void EmpfangAnmelden()
        {
            EventHandlers["toter-fitness:client:empfang"] += new Action<object>(daten =>
            {
                var d = daten as IDictionary<string, object>;
                if (d == null || beschaeftigt) return;
                Nui(new Dictionary<string, object>(d) { { "action", "empfang" } });
                SetNuiFocus(true, true);
                statsOffen = true;
            });
            EventHandlers["toter-fitness:client:angebot"] += new Action<object>(daten =>
            {
                var d = daten as IDictionary<string, object>;
                if (d == null) return;
                if (beschaeftigt)
                {
                    TriggerServerEvent("toter-fitness:server:angebotAntwort", false);
                    Hinweis("Ein Gym-Angebot kam während des Trainings und wurde abgelehnt.", "error", 4000);
                    return;
                }
                Nui(new Dictionary<string, object>(d) { { "action", "angebot" } });
                SetNuiFocus(true, true);
                statsOffen = true;
            });
            NuiCallback("empfangAnbieten", d =>
            {
                object gym, kunde, art, was;
                if (!d.TryGetValue("gymId", out gym) || !d.TryGetValue("kunde", out kunde) || !d.TryGetValue("art", out art) || !d.TryGetValue("was", out was)) return;
                TriggerServerEvent("toter-fitness:server:empfangAnbieten", Convert.ToInt32(gym), Convert.ToInt32(kunde), Convert.ToString(art), Convert.ToString(was));
            });
            NuiCallback("empfangKaufen", d =>
            {
                object gym, art, was;
                if (!d.TryGetValue("gymId", out gym) || !d.TryGetValue("art", out art) || !d.TryGetValue("was", out was)) return;
                TriggerServerEvent("toter-fitness:server:empfangKaufen", Convert.ToInt32(gym), Convert.ToString(art), Convert.ToString(was));
                SetNuiFocus(false, false);
                statsOffen = false;
            });
            NuiCallback("angebotAntwort", d =>
            {
                object ja;
                TriggerServerEvent("toter-fitness:server:angebotAntwort", d.TryGetValue("ja", out ja) && ja is bool b && b);
                SetNuiFocus(false, false);
                statsOffen = false;
            });
        }

        private async Task EmpfangTick()
        {
            if (empfaenge.Count == 0 || platzieren)
            {
                await Delay(1000);
                return;
            }
            var ped = PlayerPedId();
            var pos = GetEntityCoords(ped, true);

            if (GetGameTimer() > empfangNaechste)
            {
                empfangNaechste = GetGameTimer() + 500;
                empfangNah = 0;
                var bester = Config.MarkerAbstand;
                foreach (var e in empfaenge)
                {
                    var d = Abstand(pos, e.Value.X, e.Value.Y, e.Value.Z);
                    if (d < bester) { bester = d; empfangNah = e.Key; }
                }
            }
            if (empfangNah == 0) return;
            Vector3 p;
            if (!empfaenge.TryGetValue(empfangNah, out p)) return;

            DrawMarker(29, p.X, p.Y, p.Z, 0f, 0f, 0f, 0f, 0f, 0f, 0.55f, 0.55f, 0.55f, 25, 214, 242, 190, true, true, 2, false, null, null, false);
            if (beschaeftigt || statsOffen || Abstand(pos, p.X, p.Y, p.Z) > Config.EmpfangAbstand) return;

            string name;
            gyms.TryGetValue(empfangNah, out name);
            BeginTextCommandDisplayHelp("STRING");
            AddTextComponentSubstringPlayerName($"~INPUT_PICKUP~ Empfang {name}");
            EndTextCommandDisplayHelp(0, false, false, -1);
            if (IsControlJustPressed(0, 38)) TriggerServerEvent("toter-fitness:server:empfang", empfangNah);
        }
    }
}
