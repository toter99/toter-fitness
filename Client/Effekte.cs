using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public partial class Main
    {
        private string gesetzteGangart;
        private int gangartPed;
        private int gangartPruefung;
        private float sprintZeit;
        private int erschoepftBis;
        private int naechsterStolperer;
        private int effekteNaechste;
        private readonly Random wuerfel = new Random();

        private void Effekte(bool statsSetzen)
        {
            var spieler = PlayerId();
            var kraft = Wert("kraft");
            var ausdauer = Wert("ausdauer");
            var kondition = Wert("kondition");

            Function.Call(Hash.SET_PLAYER_MELEE_WEAPON_DAMAGE_MODIFIER, spieler, 1f + 0.2f * Plus(kraft) - 0.2f * Minus(kraft));
            SetRunSprintMultiplierForPlayer(spieler, 1f + Config.SprintTempoPlus * Plus(ausdauer));
            SetSwimMultiplierForPlayer(spieler, 1f + Config.SchwimmTempoPlus * Plus(ausdauer));

            {
                Function.Call(Hash.STAT_SET_INT, Game.GenerateHash("MP0_STRENGTH"), kraft, true);
                Function.Call(Hash.STAT_SET_INT, Game.GenerateHash("MP0_STAMINA"), (int)(Config.GtaAusdauerMax * Math.Min(1f, kondition / (float)Config.VollAb)), true);
            }
            Gangart();
            Aussehen(true);
            FettKleidung();
        }

        private string gesetztesAussehen;
        private int aussehenZeit;

        private static readonly float[,] Fett = { { 9, 0.6f }, { 10, 1f }, { 13, 0.7f }, { 19, 1f } };
        private static readonly float[,] Duenn = { { 9, -0.5f }, { 10, -1f }, { 13, -0.5f }, { 19, -0.8f } };

        private string GewuenschtesAussehen()
        {
            if (!Config.AussehenAnpassen) return null;
            if (Wert("ausdauer") < Config.SchwachUnter && Wert("kondition") < Config.SchwachUnter) return "fett";
            if (Wert("kraft") < Config.SchwachUnter) return "duenn";
            return null;
        }

        private void Aussehen(bool erzwingen)
        {
            var ped = PlayerPedId();
            var modell = (int)GetEntityModel(ped);
            if (modell != Game.GenerateHash("mp_m_freemode_01") && modell != Game.GenerateHash("mp_f_freemode_01")) return;

            var neu = GewuenschtesAussehen();
            if (neu == null)
            {
                if (gesetztesAussehen != null)
                {
                    gesetztesAussehen = null;
                    SkinNeuLaden();
                }
                return;
            }
            if (neu == gesetztesAussehen && !erzwingen) return;
            var werte = neu == "fett" ? Fett : Duenn;
            for (var i = 0; i < werte.GetLength(0); i++)
                SetPedFaceFeature(ped, (int)werte[i, 0], werte[i, 1]);
            gesetztesAussehen = neu;
        }

        private string GewuenschteGangart()
        {
            if (!Config.GangartAnpassen) return null;
            var kraft = Wert("kraft");
            var ausdauer = Wert("ausdauer");
            var kondition = Wert("kondition");
            var schnitt = (kraft + ausdauer + Wert("flexibilitaet") + kondition) / 4;
            if (ausdauer < Config.SchwachUnter && kondition < Config.SchwachUnter) return Config.GangFett;
            if (kraft < Config.SchwachUnter) return Config.GangSchwach;
            if (!string.IsNullOrEmpty(Config.GangStark) && kraft > Config.StarkUeber && schnitt > 70) return Config.GangStark;
            return null;
        }

        private async void Gangart()
        {
            try
            {
                var ped = PlayerPedId();
                if (ped != gangartPed)
                {
                    gangartPed = ped;
                    gesetzteGangart = null;
                }
                var neu = GewuenschteGangart();
                if (neu == gesetzteGangart) return;
                if (neu == null)
                {
                    ResetPedMovementClipset(ped, 0.25f);
                    gesetzteGangart = null;
                    return;
                }
                RequestAnimSet(neu);
                var bis = GetGameTimer() + 3000;
                while (!HasAnimSetLoaded(neu))
                {
                    if (GetGameTimer() > bis) return;
                    await Delay(10);
                }
                SetPedMovementClipset(ped, neu, 0.25f);
                gesetzteGangart = neu;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Gangart: {e.Message}");
            }
        }

        private static float SprintGrenze(int kondition)
        {
            if (kondition >= Config.VollAb) return float.MaxValue;
            if (kondition <= Config.PositivAb)
                return Config.SprintMinSekunden + (Config.SprintNormalSekunden - Config.SprintMinSekunden) * kondition / (float)Config.PositivAb;
            return Config.SprintNormalSekunden + (Config.SprintMaxSekunden - Config.SprintNormalSekunden) * Plus(kondition);
        }

        private static float Plus(int wert)
        {
            return Stufe(wert, Config.PositivAb);
        }

        private static float Minus(int wert)
        {
            return wert >= Config.NegativUnter ? 0f : (Config.NegativUnter - wert) / (float)Config.NegativUnter;
        }

        private static float Stufe(int wert, int ab)
        {
            if (wert <= ab) return 0f;
            return Math.Min(1f, (wert - ab) / (float)(Config.VollAb - ab));
        }

        private async void Luftholen(int ped, int kondition)
        {
            var dauer = (int)((4f + 2f * Minus(kondition) - 2f * Plus(kondition)) * 1000);
            erschoepftBis = GetGameTimer() + dauer;
            Hinweis("Du musst kurz Luft holen.", "error", 2500);
            try
            {
                if (!Config.LuftholenAnimation || IsPedRagdoll(ped) || IsPedFalling(ped)) return;
                const string dict = "re@construction";
                RequestAnimDict(dict);
                var bis = GetGameTimer() + 1500;
                while (!HasAnimDictLoaded(dict) && GetGameTimer() < bis) await Delay(20);
                if (!HasAnimDictLoaded(dict) || beschaeftigt) return;
                TaskPlayAnim(ped, dict, "out_of_breath", 4f, -4f, Math.Min(dauer, 3500), 48, 0f, false, false, false);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Luftholen: {e.Message}");
            }
        }

        private bool faellt;
        private int fallLeben, fallEnde;

        private void FallPruefen(int ped)
        {
            var jetztFaellt = IsPedFalling(ped) || (IsPedRagdoll(ped) && GetEntityHeightAboveGround(ped) > 1.5f);
            if (jetztFaellt && !faellt)
            {
                faellt = true;
                fallLeben = GetEntityHealth(ped);
                fallEnde = 0;
            }
            else if (!jetztFaellt && faellt)
            {
                faellt = false;
                fallEnde = GetGameTimer() + 1200;
            }
            if (fallEnde == 0 || GetGameTimer() < fallEnde) return;
            fallEnde = 0;
            var flex = Wert("flexibilitaet");
            var anteil = Plus(flex) * Config.FallschadenWeniger;
            var jetzt = GetEntityHealth(ped);
            if (anteil <= 0 || jetzt <= 0 || IsEntityDead(ped) || jetzt >= fallLeben) return;
            var zurueck = (int)Math.Round((fallLeben - jetzt) * anteil);
            if (zurueck > 0) SetEntityHealth(ped, Math.Min(GetEntityMaxHealth(ped), jetzt + zurueck));
        }

        private async Task EffekteTick()
        {
            var ped = PlayerPedId();

            if (GetGameTimer() < erschoepftBis)
            {
                DisableControlAction(0, 21, true);
                DisableControlAction(0, 22, true);
                return;
            }

            if (!beschaeftigt) FallPruefen(ped);

            if (muskelkaterBis != "" && !beschaeftigt && !IsPedInAnyVehicle(ped, false))
                SetPedMoveRateOverride(ped, Config.MuskelkaterTempo);

            if (Wert("kondition") >= Config.VollAb && (IsPedSprinting(ped) || IsPedRunning(ped)))
                ResetPlayerStamina(PlayerId());

            if (GetGameTimer() < effekteNaechste)
            {
                await Delay(0);
                return;
            }
            effekteNaechste = GetGameTimer() + 500;
            if (beschaeftigt || IsEntityDead(ped) || IsPedInAnyVehicle(ped, false)) return;

            try
            {
                if (GetGameTimer() > gangartPruefung)
                {
                    gangartPruefung = GetGameTimer() + 5000;
                    Gangart();
                }
                if (GetGameTimer() > aussehenZeit)
                {
                    aussehenZeit = GetGameTimer() + 10000;
                    Aussehen(true);
                    FettKleidung();
                }

                var sprintet = IsPedSprinting(ped);
                sprintZeit = sprintet ? sprintZeit + 0.5f : Math.Max(0f, sprintZeit - 1f);

                var kondition = Wert("kondition");
                if (!IsEntityInWater(ped) && sprintZeit > SprintGrenze(kondition))
                {
                    sprintZeit = 0f;
                    Luftholen(ped, kondition);
                    return;
                }

                var flex = Wert("flexibilitaet");
                if (sprintet && flex < Config.NegativUnter && GetGameTimer() > naechsterStolperer && wuerfel.NextDouble() < Minus(flex) * 0.03)
                {
                    naechsterStolperer = GetGameTimer() + 20000;
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, ped, 1000, 1500, 0, false, false, false);
                    Hinweis("Du stolperst. Ein bisschen Dehnen würde helfen.", "error", 3000);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Effekte: {e.Message}");
            }
        }
    }
}
