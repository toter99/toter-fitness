using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public partial class Main
    {
        private bool beschaeftigt;
        private string token;
        private StationC aktiv;
        private Uebung uebung;
        private bool animSteuerbar;

        private bool routeAktiv;
        private int routeIndex;
        private int routeBefehlZeit;
        private int ankuenfte;
        private float rundeLaenge;
        private float erledigt;
        private int animPruefZeit;
        private bool zuletztOben;

        private bool leitungTraining;

        private async void OnStart(object stationId, object neuerToken, object leitung)
        {
            try
            {
                if (beschaeftigt) return;
                var id = Convert.ToInt32(stationId);
                StationC station = null;
                foreach (var s in stationen) if (s.Id == id) station = s;
                if (station == null) return;
                var u = Config.UebungFuer(station.Stat, station.Art);
                if (u == null) return;

                beschaeftigt = true;
                leitungTraining = leitung is bool lb && lb;
                token = Convert.ToString(neuerToken);
                aktiv = station;
                uebung = u;
                animSteuerbar = false;
                routeAktiv = false;

                var ped = PlayerPedId();
                ClearPedTasksImmediately(ped);
                Schutz(ped, true);
                HantelnMerken(ped);
                zuletztOben = false;
                animPruefZeit = GetGameTimer() + 2500;

                FreezeEntityPosition(ped, false);
                if (u.Route && station.Route.Count >= 2)
                {
                    routeAktiv = true;
                    routeIndex = 0;
                    routeBefehlZeit = 0;
                    ankuenfte = 0;
                    erledigt = 0f;
                    rundeLaenge = 0f;
                    for (var i = 0; i < station.Route.Count; i++)
                        rundeLaenge += Strecke(station.Route[i], station.Route[(i + 1) % station.Route.Count]);
                    if (rundeLaenge < 1f) rundeLaenge = 1f;
                }
                else
                {
                    PlatzEinnehmen(ped);
                    await Delay(50);

                    if (!string.IsNullOrEmpty(u.AnimDict) && DoesAnimDictExist(u.AnimDict) && await AnimLaden(u.AnimDict))
                    {
                        TaskPlayAnim(ped, u.AnimDict, u.AnimClip, 4f, -4f, -1, 1, 0f, false, false, false);
                        animSteuerbar = true;
                        if (u.Steuerbar)
                        {
                            await Delay(300);
                            InRuhe(ped, u);
                        }
                    }
                    else if (!string.IsNullOrEmpty(u.Szenario))
                    {
                        TaskStartScenarioInPlace(ped, u.Szenario, 0, true);
                    }
                }

                Nui(new Dictionary<string, object>
                {
                    { "action", "spiel" },
                    { "spiel", u.Spiel },
                    { "titel", u.Name },
                    { "stat", Config.StatNamen[u.Stat] },
                    { "studio", u.Art == "studio" },
                    { "runde", false },
                    { "dauer", u.Dauer }
                });
                SetNuiFocus(true, false);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Training starten: {e}");
                TrainingAbbrechen();
            }
        }

        private static float Strecke(float[] a, float[] b)
        {
            var dx = a[0] - b[0]; var dy = a[1] - b[1]; var dz = a[2] - b[2];
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static void Schutz(int ped, bool an)
        {
            SetEntityInvincible(ped, an);
            Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, ped, !an);
            Function.Call(Hash.SET_PED_CAN_RAGDOLL, ped, !an);
            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped, !an);
            if (an) Function.Call(Hash.SET_ENTITY_VISIBLE, ped, true, false);
        }

        private async Task<bool> AnimLaden(string dict)
        {
            RequestAnimDict(dict);
            var bis = GetGameTimer() + 3000;
            while (!HasAnimDictLoaded(dict))
            {
                if (GetGameTimer() > bis) return false;
                await Delay(10);
            }
            return true;
        }

        private async Task TrainingTick()
        {
            if (!beschaeftigt)
            {
                await Delay(250);
                return;
            }
            try
            {
                var ped = PlayerPedId();
                if (IsEntityDead(ped))
                {
                    TrainingAbbrechen();
                    return;
                }
                var pos = GetEntityCoords(ped, true);
                RuheAnsteuern(ped);

                if (routeAktiv && aktiv != null)
                {
                    if (JoggenSchritt(ped, pos)) return;
                }
                else if (aktiv != null && Abstand(pos, aktiv.X, aktiv.Y, aktiv.Z) > 5.5f)
                {
                    TrainingAbbrechen();
                    Hinweis("Du hast die Station verlassen, das Training ist abgebrochen.", "error", 4000);
                    return;
                }
                else if (aktiv != null && uebung != null && GetGameTimer() > animPruefZeit)
                {
                    animPruefZeit = GetGameTimer() + 500;
                    UebungFortsetzen(ped);
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Training: {e.Message}");
            }
            await Delay(100);
        }

        private bool JoggenSchritt(int ped, Vector3 pos)
        {
            var route = aktiv.Route;
            var ziel = route[routeIndex];
            var rest = Abstand(pos, ziel[0], ziel[1], ziel[2]);
            if (rest < Config.RoutePunktRadius)
            {
                ankuenfte++;
                if (ankuenfte > 1) erledigt += Strecke(route[(routeIndex - 1 + route.Count) % route.Count], ziel);

                routeIndex = (routeIndex + 1) % route.Count;
                ziel = route[routeIndex];
                rest = Abstand(pos, ziel[0], ziel[1], ziel[2]);
                routeBefehlZeit = 0;
            }
            if (GetGameTimer() > routeBefehlZeit && (routeBefehlZeit == 0 || GetScriptTaskStatus(ped, 0x7D8F4411) == 7))
            {
                TaskGoStraightToCoord(ped, ziel[0], ziel[1], ziel[2], 2.0f, -1, 0f, 0f);
                routeBefehlZeit = GetGameTimer() + 1000;
            }

            return false;
        }

        private void NuiPhase(IDictionary<string, object> daten)
        {
            if (!beschaeftigt || !animSteuerbar || uebung == null || !uebung.Steuerbar) return;
            object oben;
            var hoch = daten.TryGetValue("oben", out oben) && oben is bool b && b;
            zuletztOben = hoch;
            if (hoch) SetEntityAnimSpeed(PlayerPedId(), uebung.AnimDict, uebung.AnimClip, 1f);
        }

        private float RuhePhase(Uebung u)
        {
            float p;
            return ruhePhaseTest.TryGetValue(u.Stat + ":" + u.Art, out p) ? p : u.RuhePhase;
        }

        private readonly Dictionary<string, float> ruhePhaseTest = new Dictionary<string, float>();

        private void InRuhe(int ped, Uebung u)
        {
            SetEntityAnimCurrentTime(ped, u.AnimDict, u.AnimClip, RuhePhase(u));
            SetEntityAnimSpeed(ped, u.AnimDict, u.AnimClip, 0f);
        }

        private void RuheAnsteuern(int ped)
        {
            var u = uebung;
            if (u == null || !u.Steuerbar || !animSteuerbar || zuletztOben) return;
            if (!IsEntityPlayingAnim(ped, u.AnimDict, u.AnimClip, 3)) return;
            var jetzt = GetEntityAnimCurrentTime(ped, u.AnimDict, u.AnimClip);
            var abstand = RuhePhase(u) - jetzt;
            if (abstand < 0) abstand += 1f;
            if (abstand < 0.03f || abstand > 0.985f) InRuhe(ped, u);
            else SetEntityAnimSpeed(ped, u.AnimDict, u.AnimClip, 1f);
        }

        private void UebungFortsetzen(int ped)
        {
            var u = uebung;
            if (animSteuerbar)
            {
                if (IsEntityPlayingAnim(ped, u.AnimDict, u.AnimClip, 3)) return;
                PlatzEinnehmen(ped);
                TaskPlayAnim(ped, u.AnimDict, u.AnimClip, 8f, -8f, -1, 1, 0f, false, false, false);
                if (u.Steuerbar && !zuletztOben) InRuhe(ped, u);
            }
            else if (!string.IsNullOrEmpty(u.Szenario))
            {
                if (IsPedUsingScenario(ped, u.Szenario)) return;
                PlatzEinnehmen(ped);
                TaskStartScenarioInPlace(ped, u.Szenario, 0, false);
            }
        }

        private void NuiErgebnis(IDictionary<string, object> daten)
        {
            if (!beschaeftigt) return;
            object roh;
            var punkte = 0;
            if (daten.TryGetValue("punkte", out roh))
            {
                try { punkte = Convert.ToInt32(roh); } catch { punkte = 0; }
            }
            object v;
            var verloren = daten.TryGetValue("verloren", out v) && v is bool b && b;
            object r;
            var rekord = 0;
            if (daten.TryGetValue("rekord", out r)) { try { rekord = Convert.ToInt32(r); } catch { rekord = 0; } }
            ErgebnisSenden(punkte, verloren, rekord);
        }

        private void ErgebnisSenden(int punkte, bool verloren = false, int rekord = 0)
        {
            if (!beschaeftigt) return;
            var t = token;
            var u = uebung;
            if (leitungTraining)
            {
                punkte = 100;
                verloren = false;
            }
            var patzer = (verloren || punkte < Config.PatzerUnter) && u != null;
            TrainingBeenden(!patzer);
            TriggerServerEvent("toter-fitness:server:ergebnis", t, punkte, rekord);
            if (patzer) Patzer(u);
        }

        private async void Patzer(Uebung u)
        {
            var ped = PlayerPedId();
            try
            {
                SetEntityInvincible(ped, true);
                FreezeEntityPosition(ped, false);
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, ped, true);
                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped, 2500, 3500, 0, false, false, false);
                await Delay(50);
                Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped, 1, u.PatzerSeite, u.PatzerVor, u.PatzerHoch,
                    0f, 0f, 0f, 0, true, false, true, false, true);
                if (!string.IsNullOrEmpty(u.PatzerText))
                    Hinweis(u.PatzerText, "error", 4000);
                await Delay(4000);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Patzer: {e.Message}");
            }
            if (!beschaeftigt)
            {
                Schutz(PlayerPedId(), false);
                SteuerungFreigeben(PlayerPedId());
            }
        }

        private async void NuiTreffer(IDictionary<string, object> daten)
        {
            if (!beschaeftigt || uebung == null) return;
            var u = uebung;
            object roh;
            var leben = daten.TryGetValue("leben", out roh) ? Convert.ToInt32(roh) : 2;
            if (leben <= 0) return;
            if (!routeAktiv && !string.IsNullOrEmpty(u.TrefferDict))
            {
                await AusserAtem(u, leben == 1 ? 2600 : 1600);
                return;
            }
            var staerke = (leben == 1 ? 0.45f : 0.25f) * (0.7f + (float)wuerfel.NextDouble() * 0.6f);
            var dauer = (leben == 1 ? 1100 : 650) + wuerfel.Next(0, 300);
            var ped = PlayerPedId();
            try
            {
                if (routeAktiv) routeBefehlZeit = GetGameTimer() + dauer + 600;
                animPruefZeit = GetGameTimer() + dauer + 800;
                FreezeEntityPosition(ped, false);
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, ped, true);
                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped, dauer, dauer + 400, 0, false, false, false);
                await Delay(30);
                var faktor = routeAktiv ? 1f : 0.35f;
                Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped, 1, u.PatzerSeite * staerke * faktor, u.PatzerVor * staerke * faktor,
                    Math.Max(0.3f, u.PatzerHoch * staerke), 0f, 0f, 0f, 0, true, false, true, false, true);
                await Delay(dauer + 400);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Stolpern: {e.Message}");
            }
            if (beschaeftigt) Function.Call(Hash.SET_PED_CAN_RAGDOLL, PlayerPedId(), false);
        }

        private async Task AusserAtem(Uebung u, int dauer)
        {
            var ped = PlayerPedId();
            try
            {
                if (!await AnimLaden(u.TrefferDict)) return;
                animPruefZeit = GetGameTimer() + dauer + 200;
                TaskPlayAnim(ped, u.TrefferDict, u.TrefferClip, 4f, -4f, dauer, 0, 0f, false, false, false);
                Hinweis("Puh, außer Atem.", "error", 1500);
                await Delay(dauer);
                if (beschaeftigt && uebung == u) StopAnimTask(ped, u.TrefferDict, u.TrefferClip, 2f);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Außer Atem: {e.Message}");
            }
        }

        private void TrainingAbbrechen()
        {
            var t = token;
            var warAktiv = beschaeftigt;
            TrainingBeenden();
            Nui(new Dictionary<string, object> { { "action", "abbrechen" } });
            if (warAktiv) TriggerServerEvent("toter-fitness:server:abbruch", t);
        }

        private async void Nachkontrolle()
        {
            for (var i = 0; i < 4; i++)
            {
                await Delay(400);
                if (beschaeftigt) return;
                var ped = PlayerPedId();
                if (IsPedUsingAnyScenario(ped)) ClearPedTasksImmediately(ped);
                SzenarioPropsEntfernen(ped);
                SteuerungFreigeben(ped);
            }
        }

        private void SteuerungFreigeben(int ped)
        {
            FreezeEntityPosition(ped, false);
            SetCurrentPedWeapon(ped, (uint)Game.GenerateHash("WEAPON_UNARMED"), true);
            SetPedCanSwitchWeapon(ped, true);
            ClearPedSecondaryTask(ped);
            SetPlayerControl(PlayerId(), true, 0);
            SetPedConfigFlag(ped, 122, false);
            Function.Call(Hash.SET_PED_CAN_RAGDOLL, ped, true);
            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped, true);
        }

        private List<string> Check()
        {
            var ped = PlayerPedId();
            var angehaengt = new List<string>();
            foreach (var prop in World.GetAllProps())
                if (GetEntityAttachedTo(prop.Handle) == ped) angehaengt.Add(prop.Model.Hash.ToString());
            object eigen = Game.Player.State.Get("toterFitness");
            var zeilen = new[]
            {
                $"Training läuft: {beschaeftigt}, Statebag toterFitness: {eigen ?? "leer"}",
                $"Waffe: {GetSelectedPedWeapon(ped)} (unbewaffnet = {(uint)Game.GenerateHash("WEAPON_UNARMED")}), bewaffnet: {IsPedArmed(ped, 7)}",
                $"Szenario aktiv: {IsPedUsingAnyScenario(ped)}, eingefroren: {IsEntityPositionFrozen(ped)}, Ragdoll: {IsPedRagdoll(ped)}",
                $"Steuerung an: {IsPlayerControlOn(PlayerId())}, Angriff-Taste frei: {IsControlEnabled(0, 24)}, Nahkampf aus (Flag 122): {GetPedConfigFlag(ped, 122, true)}",
                $"Angehängte Objekte: {(angehaengt.Count == 0 ? "keine" : string.Join(", ", angehaengt))}",
                $"Trainierende in der Nähe (sperren Schlagen): {trainierendeNah.Count}, außer Atem: {GetGameTimer() < erschoepftBis}",
                $"Coachen: Gym {(studio == null ? "keins" : "ja")}, Trainer/Chef {(studio != null && studio.TryGetValue("trainer", out var tr) ? tr : "?")}, " +
                $"im Dienst {(studio != null && studio.TryGetValue("dienst", out var di) ? di : "?")}"
            };
            return new List<string>(zeilen);
        }

        private void TrainingBeenden(bool schutzAus = true)
        {
            SetNuiFocus(false, false);
            var ped = PlayerPedId();
            FreezeEntityPosition(ped, false);
            if (schutzAus) Schutz(ped, false);
            if (animSteuerbar && uebung != null)
            {
                SetEntityAnimSpeed(ped, uebung.AnimDict, uebung.AnimClip, 1f);
                StopAnimTask(ped, uebung.AnimDict, uebung.AnimClip, 2f);
                RemoveAnimDict(uebung.AnimDict);
            }
            ClearPedTasksImmediately(ped);
            SzenarioPropsEntfernen(ped);
            if (schutzAus) SteuerungFreigeben(ped);
            else
            {
                SetCurrentPedWeapon(ped, (uint)Game.GenerateHash("WEAPON_UNARMED"), true);
                SetPedCanSwitchWeapon(ped, true);
            }
            beschaeftigt = false;
            Nachkontrolle();
            routeAktiv = false;
            animSteuerbar = false;
            aktiv = null;
            uebung = null;
            token = null;
        }
    }
}
