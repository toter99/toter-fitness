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
        private readonly List<int> trainierendeNah = new List<int>();
        private int trainierendeSuche;

        private static readonly HashSet<int> SzenarioModelle = new HashSet<int>
        {
            Game.GenerateHash("prop_barbell_01"), Game.GenerateHash("prop_barbell_02"),
            Game.GenerateHash("prop_barbell_10kg"), Game.GenerateHash("prop_barbell_20kg"),
            Game.GenerateHash("prop_barbell_30kg"), Game.GenerateHash("prop_barbell_40kg"),
            Game.GenerateHash("prop_barbell_50kg"), Game.GenerateHash("prop_barbell_60kg"),
            Game.GenerateHash("prop_barbell_80kg"), Game.GenerateHash("prop_barbell_100kg"),
            Game.GenerateHash("prop_curl_bar_01"), Game.GenerateHash("prop_weight_bar_01")
        };

        private async Task SchutzTick()
        {
            if (GetGameTimer() > trainierendeSuche)
            {
                trainierendeSuche = GetGameTimer() + 500;
                TrainierendeSuchen();
            }
            if (trainierendeNah.Count == 0)
            {
                await Delay(250);
                return;
            }
            var ich = PlayerPedId();
            foreach (var ped in trainierendeNah)
                SetEntityNoCollisionEntity(ich, ped, true);
            DisableControlAction(0, 24, true);
            DisableControlAction(0, 140, true);
            DisableControlAction(0, 141, true);
            DisableControlAction(0, 142, true);
            DisableControlAction(0, 257, true);
            DisableControlAction(0, 263, true);
            DisableControlAction(0, 264, true);
        }

        private void TrainierendeSuchen()
        {
            trainierendeNah.Clear();
            try
            {
                var ich = PlayerPedId();
                var pos = GetEntityCoords(ich, true);
                var gesehen = new HashSet<int>();
                foreach (var p in Players)
                {
                    var ped = p.Character?.Handle ?? 0;
                    if (ped == 0 || ped == ich) continue;
                    object status = p.State.Get("toterFitness");
                    var trainiert = status is bool b && b;
                    var andere = GetEntityCoords(ped, true);
                    var abstand = Abstand(pos, andere.X, andere.Y, andere.Z);
                    if (abstand > 60f) continue;
                    gesehen.Add(ped);

                    HashSet<int> deko;
                    var warDabei = andereTraining.TryGetValue(ped, out deko);
                    if (trainiert && !warDabei)
                        andereTraining[ped] = new HashSet<int>(HantelnInDerNaehe(ped, 6f));
                    else if (!trainiert && warDabei)
                    {
                        andereTraining.Remove(ped);
                        AndereAufraeumen(ped, deko);
                    }

                    if (trainiert && abstand < 6f) trainierendeNah.Add(ped);
                }
                foreach (var weg in new List<int>(andereTraining.Keys))
                    if (!gesehen.Contains(weg)) andereTraining.Remove(weg);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Schutz: {e.Message}");
            }
        }

        private readonly HashSet<int> hantelnVorher = new HashSet<int>();
        private readonly Dictionary<int, HashSet<int>> andereTraining = new Dictionary<int, HashSet<int>>();

        private async void AndereAufraeumen(int ped, HashSet<int> deko)
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    if (DoesEntityExist(ped))
                        foreach (var h in HantelnInDerNaehe(ped, 3f))
                            if (!deko.Contains(h)) await HantelLoeschen(h);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[{Config.Ressource}] Hanteln anderer: {e.Message}");
                }
                await Delay(500);
            }
        }

        private async Task HantelLoeschen(int handle)
        {
            var h = handle;
            if (!DoesEntityExist(h)) return;
            if (NetworkGetEntityIsNetworked(h) && !NetworkHasControlOfEntity(h))
            {
                NetworkRequestControlOfEntity(h);
                var bis = GetGameTimer() + 600;
                while (!NetworkHasControlOfEntity(h) && GetGameTimer() < bis) await Delay(50);
            }
            if (IsEntityAttached(h)) DetachEntity(h, true, false);
            SetEntityAsMissionEntity(h, false, true);
            DeleteObject(ref h);
        }

        private List<int> HantelnInDerNaehe(int ped, float radius)
        {
            var liste = new List<int>();
            var pos = GetEntityCoords(ped, true);
            foreach (var prop in World.GetAllProps())
            {
                if (!SzenarioModelle.Contains(prop.Model.Hash)) continue;
                if (props.ContainsValue(prop.Handle)) continue;
                var nah = Abstand(pos, prop.Position.X, prop.Position.Y, prop.Position.Z) < radius;
                if (nah || GetEntityAttachedTo(prop.Handle) == ped) liste.Add(prop.Handle);
            }
            return liste;
        }

        private void HantelnMerken(int ped)
        {
            hantelnVorher.Clear();
            try { foreach (var h in HantelnInDerNaehe(ped, 6f)) hantelnVorher.Add(h); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Hanteln merken: {e.Message}"); }
        }

        private void SzenarioPropsEntfernen(int ped)
        {
            try
            {
                foreach (var handle in HantelnInDerNaehe(ped, 3f))
                {
                    if (hantelnVorher.Contains(handle)) continue;
                    var h = handle;
                    if (IsEntityAttached(h)) DetachEntity(h, true, false);
                    if (NetworkGetEntityIsNetworked(h) && !NetworkHasControlOfEntity(h))
                    {
                        var _ = HantelLoeschen(h);
                        continue;
                    }
                    SetEntityAsMissionEntity(h, false, true);
                    DeleteObject(ref h);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Szenario-Props: {e.Message}");
            }
        }
    }
}
