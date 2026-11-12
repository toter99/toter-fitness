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
        private bool platzieren;
        private Uebung platzUebung;
        private string platzProp;
        private int platzStation;
        private int geist;
        private float geistHeading, geistHoehe;
        private Vector3 geistBoden;

        private const float PlatzReichweite = 12f;

        private void PlatzierenStarten(string stat, string art, string prop, int stationId)
        {
            if (!istAdmin || platzieren || beschaeftigt) return;
            var u = Config.UebungFuer(stat, art);
            if (u == null) return;
            platzUebung = u;
            platzProp = string.IsNullOrEmpty(prop) ? u.Prop : prop == "-" ? "" : prop;
            platzStation = stationId;
            geistHeading = GetEntityHeading(PlayerPedId());
            geistHoehe = 0f;

            if (stationId > 0)
            {
                var s = stationen.Find(x => x.Id == stationId);
                if (s == null) return;
                platzProp = s.Prop ?? "";
                geistHeading = s.Heading;
                geistHoehe = s.Hoehe;
            }

            SetNuiFocus(false, false);
            statsOffen = false;
            menueOffen = false;
            Nui(new Dictionary<string, object> { { "action", "menueZu" } });
            platzieren = true;
            GeistErzeugen();
            Tick += PlatzierenTick;
        }

        private async void GeistErzeugen()
        {
            if (string.IsNullOrEmpty(platzProp)) return;
            var m = new Model(platzProp);
            if (!m.IsInCdImage)
            {
                Hinweis($"Das Modell {platzProp} gibt es nicht, es wird nur der Platz angezeigt.", "error", 4000);
                return;
            }
            if (!await m.Request(5000) || !platzieren) return;
            var pos = GetEntityCoords(PlayerPedId(), true);
            geist = Function.Call<int>(Hash.CREATE_OBJECT, m.Hash, pos.X, pos.Y, pos.Z - 5f, false, false, false);
            m.MarkAsNoLongerNeeded();
            SetEntityAlpha(geist, 150, 0);
            SetEntityCollision(geist, false, false);
            FreezeEntityPosition(geist, true);
        }

        private void PlatzierenBeenden()
        {
            platzieren = false;
            Tick -= PlatzierenTick;
            if (geist != 0 && DoesEntityExist(geist)) DeleteObject(ref geist);
            geist = 0;
        }

        private Vector3 BlickPunkt()
        {
            var cam = GetGameplayCamCoord();
            var rot = GetGameplayCamRot(2);
            var x = rot.X * (float)Math.PI / 180f;
            var z = rot.Z * (float)Math.PI / 180f;
            var richtung = new Vector3(-(float)Math.Sin(z) * (float)Math.Abs(Math.Cos(x)), (float)Math.Cos(z) * (float)Math.Abs(Math.Cos(x)), (float)Math.Sin(x));
            var ped = PlayerPedId();
            var ende = new Vector3(cam.X + richtung.X * 20f, cam.Y + richtung.Y * 20f, cam.Z + richtung.Z * 20f);
            var test = StartShapeTestRay(cam.X, cam.Y, cam.Z, ende.X, ende.Y, ende.Z, 1 | 16, ped, 0);
            var getroffen = false;
            var punkt = Vector3.Zero;
            var normale = Vector3.Zero;
            var entity = 0;
            GetShapeTestResult(test, ref getroffen, ref punkt, ref normale, ref entity);

            var spieler = GetEntityCoords(ped, true);
            if (!getroffen)
            {
                var vor = GetOffsetFromEntityInWorldCoords(ped, 0f, 2.5f, 0f);
                float boden = 0f;
                punkt = GetGroundZFor_3dCoord(vor.X, vor.Y, vor.Z + 2f, ref boden, false) ? new Vector3(vor.X, vor.Y, boden) : new Vector3(vor.X, vor.Y, spieler.Z - 0.98f);
            }
            var dx = punkt.X - spieler.X;
            var dy = punkt.Y - spieler.Y;
            var weit = (float)Math.Sqrt(dx * dx + dy * dy);
            if (weit > PlatzReichweite)
            {
                punkt = new Vector3(spieler.X + dx / weit * PlatzReichweite, spieler.Y + dy / weit * PlatzReichweite, punkt.Z);
                float boden = 0f;
                if (GetGroundZFor_3dCoord(punkt.X, punkt.Y, punkt.Z + 3f, ref boden, false)) punkt = new Vector3(punkt.X, punkt.Y, boden);
            }
            return punkt;
        }

        private async Task PlatzierenTick()
        {
            if (!platzieren) return;
            try
            {
                foreach (var c in new[] { 14, 15, 16, 17, 24, 25, 37, 38, 44, 140, 141, 142, 172, 173, 174, 175, 177, 191, 194, 199, 200, 201, 202, 241, 242 })
                    DisableControlAction(0, c, true);

                var fein = IsControlPressed(0, 21);
                if (IsDisabledControlJustPressed(0, 14) || IsDisabledControlJustPressed(0, 241)) geistHeading += fein ? 1f : 7.5f;
                if (IsDisabledControlJustPressed(0, 15) || IsDisabledControlJustPressed(0, 242)) geistHeading -= fein ? 1f : 7.5f;
                if (IsDisabledControlPressed(0, 44)) geistHeading += (fein ? 15f : 90f) * GetFrameTime();
                if (IsDisabledControlPressed(0, 38)) geistHeading -= (fein ? 15f : 90f) * GetFrameTime();
                if (IsDisabledControlPressed(0, 172)) geistHoehe += (fein ? 0.05f : 0.4f) * GetFrameTime();
                if (IsDisabledControlPressed(0, 173)) geistHoehe -= (fein ? 0.05f : 0.4f) * GetFrameTime();
                geistHoehe = Math.Max(-1f, Math.Min(2f, geistHoehe));
                geistHeading = (geistHeading % 360f + 360f) % 360f;

                geistBoden = BlickPunkt();
                if (geist != 0 && DoesEntityExist(geist))
                {
                    SetEntityCoordsNoOffset(geist, geistBoden.X, geistBoden.Y, geistBoden.Z + geistHoehe, false, false, false);
                    SetEntityHeading(geist, geistHeading);
                }

                Vector3 ped;
                float blick;
                PlatzAmProp(geist, new Vector3(geistBoden.X, geistBoden.Y, geistBoden.Z + 0.98f), geistHeading, platzUebung, out ped, out blick);
                var fuss = ped.Z - 0.98f + 0.03f;
                DrawMarker(25, ped.X, ped.Y, fuss, 0f, 0f, 0f, 0f, 0f, 0f, 0.9f, 0.9f, 1f, 25, 214, 242, 180, false, false, 2, false, null, null, false);
                Pfeil(new Vector3(ped.X, ped.Y, fuss + 0.05f), blick, 1.4f, 25, 214, 242);
                if (geist == 0) Pfeil(new Vector3(geistBoden.X, geistBoden.Y, geistBoden.Z + 0.08f), geistHeading, 0.8f, 255, 200, 60);

                Hilfe($"{platzUebung.Name}: Mausrad oder Q/E drehen ({geistHeading:0}°), Pfeil hoch/runter Höhe ({geistHoehe:+0.00;-0.00;0} m), Umschalt fein.\n~g~Enter~s~ setzen, ~r~Rücktaste~s~ abbrechen. Blauer Pfeil: Blickrichtung beim Training.");

                if (IsDisabledControlJustPressed(0, 191) || IsDisabledControlJustPressed(0, 201))
                {
                    TriggerServerEvent("toter-fitness:server:gymAktion", "platzieren", new Dictionary<string, object>
                    {
                        { "station", platzStation },
                        { "stat", platzUebung.Stat },
                        { "art", platzUebung.Art },
                        { "prop", platzStation > 0 ? "" : (platzProp == platzUebung.Prop ? "" : (platzProp == "" ? "-" : platzProp)) },
                        { "x", geistBoden.X }, { "y", geistBoden.Y }, { "z", geistBoden.Z + 0.98f },
                        { "heading", geistHeading }, { "hoehe", geistHoehe }
                    });
                    PlatzierenBeenden();
                }
                else if (IsDisabledControlJustPressed(0, 177) || IsDisabledControlJustPressed(0, 194) || IsDisabledControlJustPressed(0, 200))
                {
                    PlatzierenBeenden();
                    Hinweis("Platzieren abgebrochen.", "primary", 2000);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Platzieren: {e.Message}");
                PlatzierenBeenden();
            }
            await Task.FromResult(0);
        }

        private static void Pfeil(Vector3 von, float heading, float laenge, int r, int g, int b)
        {
            var rad = heading * (float)Math.PI / 180f;
            var vx = -(float)Math.Sin(rad);
            var vy = (float)Math.Cos(rad);
            var bis = new Vector3(von.X + vx * laenge, von.Y + vy * laenge, von.Z);
            for (var i = -1; i <= 1; i++)
            {
                var o = i * 0.015f;
                DrawLine(von.X + vy * o, von.Y - vx * o, von.Z, bis.X + vy * o, bis.Y - vx * o, bis.Z, r, g, b, 255);
            }
            foreach (var seite in new[] { 1f, -1f })
            {
                var a = rad + seite * 2.6f;
                DrawLine(bis.X, bis.Y, bis.Z, bis.X - (float)Math.Sin(a) * 0.35f, bis.Y + (float)Math.Cos(a) * 0.35f, bis.Z, r, g, b, 255);
            }
        }

        // GTA schneidet jedes Textstück nach 99 Zeichen ab, deshalb lange Hilfen in Stücke teilen.
        private static void Hilfe(string text)
        {
            BeginTextCommandDisplayHelp("STRING");
            foreach (var stueck in Stuecke(text, 90)) AddTextComponentSubstringPlayerName(stueck);
            EndTextCommandDisplayHelp(0, false, false, -1);
        }

        // Bewusst ohne yield: Iteratoren nutzen Environment.CurrentManagedThreadId, das FiveM im Client sperrt.
        private static List<string> Stuecke(string text, int laenge)
        {
            var liste = new List<string>();
            var rest = text ?? "";
            while (rest.Length > laenge)
            {
                var schnitt = rest.LastIndexOfAny(new[] { ' ', '\n' }, laenge);
                if (schnitt <= 0) schnitt = laenge;
                liste.Add(rest.Substring(0, schnitt + 1));
                rest = rest.Substring(schnitt + 1);
            }
            if (rest.Length > 0) liste.Add(rest);
            return liste;
        }
    }
}
