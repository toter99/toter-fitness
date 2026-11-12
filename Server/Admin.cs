using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Server
{
    public partial class Main
    {
        [EventHandler("toter-fitness:server:adminPruefen")]
        private void OnAdminPruefen([FromSource] Player player)
        {
            var admin = IsPlayerAceAllowed(player.Handle, "command.fitnessplace");
            player.State.Set("toterFitnessAdmin", admin, true);
            TriggerClientEvent(player, "toter-fitness:client:admin", admin);
        }

        [EventHandler("toter-fitness:server:adminBefehl")]
        private void OnAdminBefehl([FromSource] Player player, string befehl, object argsRoh)
        {
            var args = new List<object>();
            if (argsRoh is System.Collections.IEnumerable liste && !(argsRoh is string))
                foreach (var a in liste) args.Add(Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture));

            var befehle = new Dictionary<string, Action<int, List<object>, string>>
            {
                { "fitnessplace", BefehlSetzen }, { "fitnessdelete", BefehlLoeschen }, { "fitnessmove", BefehlVerschieben },
                { "fitnesshoehe", BefehlHoehe }, { "fitnessdrehen", BefehlDrehen }, { "fitnessroute", BefehlRoute },
                { "fitnessset", BefehlWerteSetzen }
            };
            Action<int, List<object>, string> ausfuehren;
            if (befehl == null || !befehle.TryGetValue(befehl, out ausfuehren)) return;
            if (!IsPlayerAceAllowed(player.Handle, "command." + befehl))
            {
                Melden(player, "Dafür fehlen dir die Rechte.", "error");
                Debug.WriteLine($"[{Config.Ressource}] {player.Name} wollte ohne Rechte {befehl} ausführen.");
                return;
            }
            Debug.WriteLine($"[{Config.Ressource}] Menü: {player.Name} -> {befehl} {string.Join(" ", args)}");
            ausfuehren(int.Parse(player.Handle), args, befehl + " " + string.Join(" ", args));
        }
    }
}
