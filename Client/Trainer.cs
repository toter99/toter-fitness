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
        // Am Boden knien, sonst mit verschränkten Armen zuschauen (ruhig, ohne Klatschen und Rufen).
        private const string KnieDict = "amb@medic@standing@kneel@base", KnieAnim = "base";
        private const string ZuschauenDict = "amb@world_human_hang_out_street@female_arms_crossed@idle_a", ZuschauenAnim = "idle_a";

        private int coachZiel;
        private string coachDict, coachAnim;
        private int coachKunde, coachSeit;

        // Trainer oder Chef des Gyms, Dienst egal (ohne Dienst sagt der Server beim Coachen Bescheid).
        private bool TrainerRang()
        {
            object k;
            return studio != null && studio.TryGetValue("kurse", out k) && k is bool kb && kb;
        }

        private bool? zielStatus;

        private void ZielStatusPruefen()
        {
            var darf = TrainerRang() && !beschaeftigt;
            if (zielStatus == darf) return;
            zielStatus = darf;
            ZielStatusMelden(darf);
        }

        private async void Coachen(int kundeServerId, bool amBoden, bool an)
        {
            if (an && beschaeftigt) return;
            var ped = PlayerPedId();
            if (!an || kundeServerId == 0)
            {
                CoachAnimStoppen(ped);
                return;
            }
            var spieler = GetPlayerFromServerId(kundeServerId);
            if (spieler < 0 || beschaeftigt) return;
            var ziel = GetPlayerPed(spieler);
            if (ziel == 0 || IsPedInAnyVehicle(ped, false)) return;

            coachZiel = ziel;
            coachKunde = kundeServerId;
            coachSeit = GetGameTimer();
            coachDict = amBoden ? KnieDict : ZuschauenDict;
            coachAnim = amBoden ? KnieAnim : ZuschauenAnim;
            try
            {
                TaskTurnPedToFaceEntity(ped, ziel, 900);
                await Delay(900);
                if (!await AnimLaden(coachDict) || coachZiel != ziel) return;
                TaskPlayAnim(ped, coachDict, coachAnim, 3f, -3f, -1, 1, 0f, false, false, false);
                coachSeit = GetGameTimer();
                Tick -= CoachTick;
                Tick += CoachTick;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Coachen: {e.Message}");
            }
        }

        private async Task CoachTick()
        {
            var ped = PlayerPedId();
            if (coachZiel == 0 || coachDict == null)
            {
                Tick -= CoachTick;
                return;
            }
            var bewegt = IsControlPressed(0, 30) || IsControlPressed(0, 31) || IsControlPressed(0, 22) || IsControlPressed(0, 21);
            var z = GetEntityCoords(coachZiel, true);
            var p = GetEntityCoords(ped, true);
            // Trainiert der Kunde nicht mehr (State Bag vom Server), hört der Trainer auch auf. Kurz nach dem Start
            // nicht prüfen, der State Bag kommt evtl. etwas später an.
            var kundeTrainiert = GetStateBagValue("player:" + coachKunde, "toterFitness") is bool tb && tb;
            // Die Animation braucht nach dem Start ein paar Frames, bis IsEntityPlayingAnim sie meldet. Vorher nicht abbrechen,
            // sonst endet nur die Prüfung und die Animation läuft endlos weiter.
            var anlauf = GetGameTimer() - coachSeit < 2000;
            var fertig = !anlauf && !kundeTrainiert;
            var weg = !anlauf && !IsEntityPlayingAnim(ped, coachDict, coachAnim, 3);
            if (bewegt || fertig || weg || Abstand(p, z.X, z.Y, z.Z) > 12f)
                CoachAnimStoppen(ped);
            await Delay(100);
        }

        private void CoachAnimStoppen(int ped)
        {
            // Immer stoppen, auch wenn IsEntityPlayingAnim gerade nichts meldet.
            if (coachDict != null)
            {
                StopAnimTask(ped, coachDict, coachAnim, 2f);
                if (!IsPedInAnyVehicle(ped, false)) ClearPedTasks(ped);
            }
            coachZiel = 0;
            coachKunde = 0;
            coachDict = null;
            coachAnim = null;
            Tick -= CoachTick;
        }

        // Coachen startet nur über das Alt-Menü (Client/ziel.lua). Hier wird nur gemeldet, ob die Option zu sehen ist.
        private async Task CoachPromptTick()
        {
            ZielStatusPruefen();
            await Delay(500);
        }

    }
}
