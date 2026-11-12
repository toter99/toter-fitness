using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public class StationC
    {
        public int Id;
        public string Name, Stat, Art, Prop;
        public float X, Y, Z, Heading, Hoehe;
        public int GymId, KursId, Platz;
        public bool Leitung;
        public List<float[]> Route = new List<float[]>();

        public float Boden { get { return Z - 0.98f; } }
    }

    public partial class Main
    {
        private List<StationC> stationen = new List<StationC>();
        private readonly List<StationC> nahe = new List<StationC>();
        private readonly Dictionary<int, int> props = new Dictionary<int, int>();
        private readonly HashSet<string> fehlendeModelle = new HashSet<string>();
        private StationC naechste;
        private int routeZeigenId;
        private int routeZeigenBis;
        private int effekteZeit;

        private void OnStationen(object daten)
        {
            var neu = new List<StationC>();
            if (daten is IEnumerable liste)
            {
                foreach (var e in liste)
                {
                    var d = e as IDictionary<string, object>;
                    if (d == null) continue;
                    try
                    {
                    var s = new StationC
                    {
                        Id = Convert.ToInt32(d["id"]),
                        Name = Convert.ToString(d["name"]),
                        Stat = Convert.ToString(d["stat"]),
                        Art = Convert.ToString(d["art"]),
                        Prop = Convert.ToString(d["prop"]),
                        X = Convert.ToSingle(d["x"]),
                        Y = Convert.ToSingle(d["y"]),
                        Z = Convert.ToSingle(d["z"]),
                        Heading = Convert.ToSingle(d["heading"]),
                        Hoehe = Convert.ToSingle(d["hoehe"]),
                        GymId = d.ContainsKey("gym") ? Convert.ToInt32(d["gym"]) : 0,
                        KursId = d.ContainsKey("kurs") ? Convert.ToInt32(d["kurs"]) : 0,
                        Platz = d.ContainsKey("platz") ? Convert.ToInt32(d["platz"]) : 0,
                        Leitung = d.ContainsKey("leitung") && d["leitung"] is bool lb && lb
                    };
                    object route;
                    if (d.TryGetValue("route", out route) && route is IEnumerable punkte)
                    {
                        foreach (var p in punkte)
                        {
                            var xyz = new List<float>();
                            if (p is IEnumerable werte)
                                foreach (var w in werte) xyz.Add(Convert.ToSingle(w));
                            if (xyz.Count == 3) s.Route.Add(xyz.ToArray());
                        }
                    }
                    neu.Add(s);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{Config.Ressource}] Station nicht lesbar: {ex.Message}");
                    }
                }
            }
            AllePropsEntfernen();
            stationen = neu;
            nahe.Clear();
        }

        private void OnRouteZeigen(object id)
        {
            routeZeigenId = Convert.ToInt32(id);
            routeZeigenBis = GetGameTimer() + 30000;
        }

        private static float Abstand(Vector3 a, float x, float y, float z)
        {
            var dx = a.X - x; var dy = a.Y - y; var dz = a.Z - z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private async Task SucheTick()
        {
            await Delay(500);
            try
            {
                var pos = GetEntityCoords(PlayerPedId(), true);
                nahe.Clear();
                var liste = stationen;
                foreach (var s in liste)
                {
                    if (liste != stationen) break;
                    var d = Abstand(pos, s.X, s.Y, s.Z);
                    if (d < Config.MarkerAbstand) nahe.Add(s);

                    int handle;
                    var hatProp = props.TryGetValue(s.Id, out handle);
                    if (!string.IsNullOrEmpty(s.Prop) && d < Config.PropAbstand && !hatProp)
                        await PropErzeugen(s);
                    else if (hatProp && d > Config.PropAbstand + 20f)
                        PropEntfernen(s.Id);
                }

                if (GetGameTimer() > effekteZeit)
                {
                    effekteZeit = GetGameTimer() + 30000;
                    Effekte(false);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Suche: {e.Message}");
            }
        }

        private async Task PropErzeugen(StationC s)
        {
            props[s.Id] = 0;
            var model = new Model(s.Prop);
            if (!model.IsInCdImage)
            {
                if (fehlendeModelle.Add(s.Prop))
                    Debug.WriteLine($"[{Config.Ressource}] Prop {s.Prop} (Station {s.Id}) gibt es nicht. Im Admin-Bereich ein anderes wählen.");
                return;
            }
            if (!await model.Request(5000))
            {
                PlatzhalterFreigeben(s.Id);
                return;
            }
            var handle = Function.Call<int>(Hash.CREATE_OBJECT, model.Hash, s.X, s.Y, s.Boden + s.Hoehe, false, false, false);
            model.MarkAsNoLongerNeeded();
            if (handle == 0)
            {
                PlatzhalterFreigeben(s.Id);
                return;
            }
            SetEntityHeading(handle, s.Heading);
            if (s.KursId != 0)
            {
                float boden, propZ;
                MatteAufBoden((uint)model.Hash, s.X, s.Y, s.Boden, s.Heading, out boden, out propZ);
                if (boden > float.MinValue) SetEntityCoordsNoOffset(handle, s.X, s.Y, propZ, false, false, false);
                PlaceObjectOnGroundProperly(handle);
                if (s.Hoehe != 0f)
                {
                    var hier = GetEntityCoords(handle, false);
                    SetEntityCoordsNoOffset(handle, hier.X, hier.Y, hier.Z + s.Hoehe, false, false, false);
                }
            }
            FreezeEntityPosition(handle, true);
            if (!stationen.Contains(s))
            {
                DeleteObject(ref handle);
                PlatzhalterFreigeben(s.Id);
                return;
            }
            props[s.Id] = handle;
        }

        private void PlatzhalterFreigeben(int id)
        {
            int h;
            if (props.TryGetValue(id, out h) && h == 0) props.Remove(id);
        }

        private void PropEntfernen(int id)
        {
            int handle;
            if (!props.TryGetValue(id, out handle)) return;
            props.Remove(id);
            if (handle != 0 && DoesEntityExist(handle))
            {
                SetEntityAsMissionEntity(handle, false, true);
                DeleteObject(ref handle);
            }
        }

        private void AllePropsEntfernen()
        {
            foreach (var id in new List<int>(props.Keys)) PropEntfernen(id);
        }

        private async Task ZeichnenTick()
        {
            if (nahe.Count == 0 && GetGameTimer() > routeZeigenBis)
            {
                await Delay(500);
                return;
            }
            try
            {
                var ped = PlayerPedId();
                var pos = GetEntityCoords(ped, true);
                naechste = null;
                var bester = float.MaxValue;

                foreach (var s in nahe)
                {
                    var studio = s.Art == "studio";
                    if (s.KursId != 0)
                        DrawMarker(25, s.X, s.Y, s.Boden + 0.02f, 0f, 0f, 0f, 0f, 0f, 0f, 1.2f, 1.2f, 1f,
                            s.Leitung ? 255 : 175, s.Leitung ? 200 : 82, s.Leitung ? 0 : 222, 150, false, false, 2, false, null, null, false);
                    else
                        DrawMarker(25, s.X, s.Y, s.Boden + 0.02f, 0f, 0f, 0f, 0f, 0f, 0f, 1.6f, 1.6f, 1f,
                            studio ? 25 : 230, studio ? 214 : 40, studio ? 242 : 40, 170, false, false, 2, false, null, null, false);
                    var d = Abstand(pos, s.X, s.Y, s.Z);
                    if (d < Config.InteraktionAbstand && d < bester)
                    {
                        bester = d;
                        naechste = s;
                    }
                }

                if (GetGameTimer() < routeZeigenBis) RouteZeichnen();

                if (naechste != null && !beschaeftigt && !mattenLegen && !IsPedInAnyVehicle(ped, false) && !IsEntityDead(ped))
                {
                    var uebung = Config.UebungFuer(naechste.Stat, naechste.Art);
                    if (uebung != null)
                    {
                        var gym = naechste.Art == "studio" ? GymName(naechste.GymId) : null;
                        var preis = gym != null ? $" ({gym}, nur Mitglieder)" : naechste.Art == "studio" ? $" (${Config.PreisStudio})" : "";
                        BeginTextCommandDisplayHelp("STRING");
                        if (naechste.KursId != 0)
                            AddTextComponentSubstringPlayerName(naechste.Leitung
                                ? $"~INPUT_PICKUP~ {uebung.Name}: Platz der Kursleitung"
                                : $"~INPUT_PICKUP~ {uebung.Name}: Platz einnehmen");
                        else
                            AddTextComponentSubstringPlayerName($"~INPUT_PICKUP~ {uebung.Name}{preis}\n{Config.StatNamen[naechste.Stat]} trainieren");
                        EndTextCommandDisplayHelp(0, false, true, -1);
                        if (IsControlJustReleased(0, 38))
                        {
                            if (!IsEntityVisible(ped))
                                Hinweis("Du bist gerade unsichtbar. Schalte Noclip oder Unsichtbarkeit aus.", "error", 4000);
                            else
                                TriggerServerEvent("toter-fitness:server:start", naechste.Id);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Zeichnen: {e.Message}");
                await Delay(1000);
            }
        }

        private void RouteZeichnen()
        {
            StationC s = null;
            foreach (var x in stationen) if (x.Id == routeZeigenId) s = x;
            if (s == null) return;
            for (var i = 0; i < s.Route.Count; i++)
            {
                var p = s.Route[i];
                DrawMarker(1, p[0], p[1], p[2] - 0.98f, 0f, 0f, 0f, 0f, 0f, 0f, 1.2f, 1.2f, 0.6f,
                    255, 210, 30, 140, false, false, 2, false, null, null, false);
                var n = s.Route[(i + 1) % s.Route.Count];
                DrawLine(p[0], p[1], p[2], n[0], n[1], n[2], 255, 210, 30, 220);
            }
        }
    }
}
