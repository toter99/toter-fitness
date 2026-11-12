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
    // Matten für einen Kurs selbst legen: Vorschau vor dem Blick, drehen, setzen. Erste Matte = Kursleitung.
    public partial class Main
    {
        private bool mattenLegen;
        private int mattenKurs, mattenMax;
        private Vector3 mattenMitte;
        private Uebung mattenUebung;
        private int mattenGeist;
        private float mattenHeading;
        private bool mattenGedreht;
        private readonly List<float[]> mattenGelegt = new List<float[]>();   // x, y, z (Boden + 0.98), heading
        private readonly List<int> mattenObjekte = new List<int>();
        private int mattenVorhanden;   // schon gelegte Matten von vorher, die bleiben liegen
        private float mattenHoehe;     // Pfeil hoch/runter, gilt für die nächsten Matten weiter
        private int mattenTastenAb;    // Spielzeit, ab der Tasten zählen (die Taste, die das Handy schließt, soll nicht abbrechen)

        private async void MattenLegenStarten(IDictionary<string, object> kurs)
        {
            if (mattenLegen || platzieren || beschaeftigt || kurs == null) return;
            object schluessel, id, max, kx, ky, kz;
            kurs.TryGetValue("schluessel", out schluessel);
            kurs.TryGetValue("id", out id);
            kurs.TryGetValue("max", out max);
            kurs.TryGetValue("kx", out kx);
            kurs.TryGetValue("ky", out ky);
            kurs.TryGetValue("kz", out kz);
            var key = Convert.ToString(schluessel);
            if (key == null || !Config.Uebungen.ContainsKey(key) || id == null) return;

            mattenUebung = Config.Uebungen[key];
            mattenKurs = Convert.ToInt32(id);
            mattenMax = Convert.ToInt32(max ?? 10);
            mattenMitte = new Vector3(Convert.ToSingle(kx ?? 0f), Convert.ToSingle(ky ?? 0f), Convert.ToSingle(kz ?? 0f));
            if (Abstand(GetEntityCoords(PlayerPedId(), true), mattenMitte.X, mattenMitte.Y, mattenMitte.Z) > Config.AufbauAbstand)
            {
                Hinweis("Geh zum Kursort, dort legst du die Matten.", "error", 4000);
                return;
            }

            mattenLegen = true;
            mattenGelegt.Clear();
            mattenObjekte.Clear();
            mattenGedreht = false;
            // Weitermachen: was schon liegt, bleibt liegen (Kursleitung zuerst).
            foreach (var s in stationen.Where(s => s.KursId == mattenKurs).OrderBy(s => s.Leitung ? 0 : 1).ThenBy(s => s.Platz))
            {
                mattenGelegt.Add(new[] { s.X, s.Y, s.Z, s.Heading, s.Hoehe });
                mattenObjekte.Add(0);
            }
            mattenVorhanden = mattenGelegt.Count;
            if (mattenVorhanden > mattenMax)
            {
                mattenLegen = false;
                Hinweis("Alle Matten liegen schon. Zum Neu-Legen erst abbauen.", "primary", 4000);
                return;
            }
            HandyZu();
            var bis = GetGameTimer() + 20000;
            while (IsNuiFocused() && GetGameTimer() < bis) await Delay(100);
            if (IsNuiFocused())
            {
                mattenLegen = false;
                Hinweis("Leg das Handy weg, dann geht es los.", "error", 4000);
                return;
            }

            var m = new Model(mattenUebung.Prop);
            if (m.IsInCdImage && await m.Request(5000))
            {
                var pos = GetEntityCoords(PlayerPedId(), true);
                mattenGeist = Function.Call<int>(Hash.CREATE_OBJECT, m.Hash, pos.X, pos.Y, pos.Z - 5f, false, false, false);
                SetEntityAlpha(mattenGeist, 160, 0);
                SetEntityCollision(mattenGeist, false, false);
                FreezeEntityPosition(mattenGeist, true);
            }
            mattenTastenAb = GetGameTimer() + 800;
            Tick += MattenTick;
        }

        private void MattenLegenBeenden(bool senden)
        {
            Tick -= MattenTick;
            if (senden && mattenGelegt.Count > 0)
                TriggerServerEvent("toter-fitness:server:kursMatten", mattenKurs, mattenGelegt.Select(m => (object)new Dictionary<string, object>
                    { { "x", m[0] }, { "y", m[1] }, { "z", m[2] }, { "h", m[3] }, { "hoehe", m.Length > 4 ? m[4] : 0f } }).ToList());
            if (mattenGeist != 0 && DoesEntityExist(mattenGeist)) DeleteObject(ref mattenGeist);
            mattenGeist = 0;
            for (var i = 0; i < mattenObjekte.Count; i++)
            {
                var o = mattenObjekte[i];
                if (o != 0 && DoesEntityExist(o)) DeleteObject(ref o);
            }
            mattenObjekte.Clear();
            mattenGelegt.Clear();
            mattenLegen = false;
        }

        // Höhe so, dass die Unterseite der Matte überall über dem Boden liegt. false = kein Boden oder zu schräg.
        private static bool MatteAufBoden(uint modell, float x, float y, float zSuche, float heading, out float boden, out float propZ)
        {
            var min = Vector3.Zero;
            var max = Vector3.Zero;
            GetModelDimensions(modell, ref min, ref max);
            var rad = heading * (float)Math.PI / 180f;
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            var hoch = float.MinValue;
            var tief = float.MaxValue;
            var mitte = float.MinValue;
            foreach (var p in new[] { new[] { 0f, 0f }, new[] { min.X, min.Y }, new[] { min.X, max.Y }, new[] { max.X, min.Y }, new[] { max.X, max.Y } })
            {
                float px = x + p[0] * c - p[1] * s, py = y + p[0] * s + p[1] * c, z = 0f;
                if (!GetGroundZFor_3dCoord(px, py, zSuche + 1.5f, ref z, false)) continue;
                if (p[0] == 0f && p[1] == 0f) mitte = z;
                hoch = Math.Max(hoch, z);
                tief = Math.Min(tief, z);
            }
            boden = mitte > float.MinValue ? mitte : hoch;
            propZ = boden - min.Z;
            return hoch > float.MinValue && hoch - tief <= Config.MatteMaxSchraeg;
        }

        private static float RichtungZu(float vonX, float vonY, float zuX, float zuY)
        {
            return ((float)(Math.Atan2(-(zuX - vonX), zuY - vonY) * 180.0 / Math.PI) + 360f) % 360f;
        }

        private async Task MattenTick()
        {
            if (!mattenLegen) return;
            try
            {
                foreach (var c in new[] { 14, 15, 16, 17, 24, 25, 37, 38, 44, 73, 140, 141, 142, 172, 173, 174, 175, 177, 191, 194, 199, 200, 201, 202, 241, 242 })
                    DisableControlAction(0, c, true);

                var ped = PlayerPedId();
                var spieler = GetEntityCoords(ped, true);
                var nr = mattenGelegt.Count;
                var leitung = nr == 0;
                var fein = IsControlPressed(0, 21);
                var punkt = BlickPunkt();

                // Startrichtung: Kursleitung schaut wie du, Teilnehmer schauen zur Kursleitung.
                if (!mattenGedreht)
                {
                    var ziel = leitung ? GetEntityHeading(ped) : RichtungZu(punkt.X, punkt.Y, mattenGelegt[0][0], mattenGelegt[0][1]);
                    var jetzt = mattenGeist != 0 && DoesEntityExist(mattenGeist) ? GetEntityHeading(mattenGeist) : 0f;
                    Vector3 p0;
                    float blick;
                    PlatzAmProp(mattenGeist, punkt, jetzt, mattenUebung, out p0, out blick);
                    mattenHeading = ((ziel - (blick - jetzt)) % 360f + 360f) % 360f;
                }
                if (IsDisabledControlJustPressed(0, 14) || IsDisabledControlJustPressed(0, 241)) { mattenHeading += fein ? 1f : 7.5f; mattenGedreht = true; }
                if (IsDisabledControlJustPressed(0, 15) || IsDisabledControlJustPressed(0, 242)) { mattenHeading -= fein ? 1f : 7.5f; mattenGedreht = true; }
                if (IsDisabledControlPressed(0, 174)) { mattenHeading += (fein ? 15f : 90f) * GetFrameTime(); mattenGedreht = true; }
                if (IsDisabledControlPressed(0, 175)) { mattenHeading -= (fein ? 15f : 90f) * GetFrameTime(); mattenGedreht = true; }
                mattenHeading = (mattenHeading % 360f + 360f) % 360f;
                if (IsDisabledControlPressed(0, 172)) mattenHoehe += (fein ? 0.05f : 0.3f) * GetFrameTime();
                if (IsDisabledControlPressed(0, 173)) mattenHoehe -= (fein ? 0.05f : 0.3f) * GetFrameTime();
                mattenHoehe = Math.Max(-0.3f, Math.Min(1f, mattenHoehe));

                var modell = (uint)new Model(mattenUebung.Prop).Hash;
                float boden, propZ;
                var ok = MatteAufBoden(modell, punkt.X, punkt.Y, punkt.Z, mattenHeading, out boden, out propZ);
                var grund = "";
                if (!ok) grund = boden > float.MinValue ? "Boden zu schräg" : "kein Boden";
                else if (Math.Abs(boden - (spieler.Z - 0.98f)) > 2f) grund = "zu hoch oder zu tief";
                else if (Abstand(new Vector3(punkt.X, punkt.Y, boden), mattenMitte.X, mattenMitte.Y, mattenMitte.Z) > Config.KursRadius) grund = "zu weit vom Kursort";
                else if (mattenGelegt.Any(m => Abstand(new Vector3(punkt.X, punkt.Y, boden + 0.98f), m[0], m[1], m[2]) < Config.MattenMinAbstand)) grund = "zu nah an einer Matte";
                if (!ok) propZ = punkt.Z + 0.01f;

                if (mattenGeist != 0 && DoesEntityExist(mattenGeist))
                {
                    SetEntityCoordsNoOffset(mattenGeist, punkt.X, punkt.Y, propZ + mattenHoehe, false, false, false);
                    SetEntityHeading(mattenGeist, mattenHeading);
                }
                Vector3 platz;
                float blickRichtung;
                PlatzAmProp(mattenGeist, new Vector3(punkt.X, punkt.Y, boden + 0.98f), mattenHeading, mattenUebung, out platz, out blickRichtung);
                var fuss = (ok ? boden : punkt.Z) + mattenHoehe + 0.04f;
                int r = grund == "" ? (leitung ? 255 : 175) : 230, g = grund == "" ? (leitung ? 200 : 82) : 40, b = grund == "" ? (leitung ? 0 : 222) : 40;
                DrawMarker(25, punkt.X, punkt.Y, fuss, 0f, 0f, 0f, 0f, 0f, 0f, 1.1f, 1.1f, 1f, r, g, b, 150, false, false, 2, false, null, null, false);
                Pfeil(new Vector3(platz.X, platz.Y, fuss + 0.05f), blickRichtung, 1.3f, r, g, b);

                var titel = leitung ? "Deine Matte (Kursleitung)" : $"Matte {nr} von {mattenMax}";
                Hilfe($"{titel}{(grund == "" ? "" : $": ~r~{grund}~s~")}\n" +
                      "~INPUT_PICKUP~ [E] Matte hinlegen\n" +
                      "~INPUT_FRONTEND_ACCEPT~ [Enter] fertig, auch früher. Später machst du an derselben Stelle weiter.\n" +
                      "Drehen: Mausrad oder Pfeiltasten links/rechts (Umschalt fein)\n" +
                      $"Höhe: Pfeiltasten hoch/runter ({mattenHoehe:+0.00;-0.00;0} m)\n[X] letzte zurück, [Rücktaste] abbrechen");

                if (GetGameTimer() < mattenTastenAb) { }
                else if (IsDisabledControlJustPressed(0, 38) && grund == "")
                {
                    mattenGelegt.Add(new[] { punkt.X, punkt.Y, boden + 0.98f, mattenHeading, mattenHoehe });
                    var m = new Model(mattenUebung.Prop);
                    if (HasModelLoaded((uint)m.Hash))
                    {
                        var o = Function.Call<int>(Hash.CREATE_OBJECT, m.Hash, punkt.X, punkt.Y, propZ, false, false, false);
                        SetEntityHeading(o, mattenHeading);
                        PlaceObjectOnGroundProperly(o);
                        if (mattenHoehe != 0f)
                        {
                            var hier = GetEntityCoords(o, false);
                            SetEntityCoordsNoOffset(o, hier.X, hier.Y, hier.Z + mattenHoehe, false, false, false);
                        }
                        FreezeEntityPosition(o, true);
                        mattenObjekte.Add(o);
                    }
                    else mattenObjekte.Add(0);
                    mattenGedreht = false;
                    if (mattenGelegt.Count > mattenMax) MattenLegenBeenden(true);
                }
                else if (IsDisabledControlJustPressed(0, 73) && mattenGelegt.Count > mattenVorhanden)
                {
                    mattenGelegt.RemoveAt(mattenGelegt.Count - 1);
                    var o = mattenObjekte[mattenObjekte.Count - 1];
                    mattenObjekte.RemoveAt(mattenObjekte.Count - 1);
                    if (o != 0 && DoesEntityExist(o)) DeleteObject(ref o);
                    mattenGedreht = false;
                }
                else if (IsDisabledControlJustPressed(0, 191) || IsDisabledControlJustPressed(0, 201))
                {
                    if (mattenGelegt.Count == mattenVorhanden) MattenLegenBeenden(false);
                    else MattenLegenBeenden(true);
                }
                else if (IsDisabledControlJustPressed(0, 177) || IsDisabledControlJustPressed(0, 194) || IsDisabledControlJustPressed(0, 200) || IsDisabledControlJustPressed(0, 202))
                {
                    MattenLegenBeenden(false);
                    Hinweis("Matten legen abgebrochen.", "primary", 2000);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Matten legen: {e.Message}");
                MattenLegenBeenden(false);
            }
            await Task.FromResult(0);
        }
    }
}
