using System;
using System.Collections.Generic;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public partial class Main
    {
        private readonly Dictionary<string, float[]> versatz = new Dictionary<string, float[]>();

        private float[] Versatz(Uebung u)
        {
            float[] v;
            if (versatz.TryGetValue(u.Stat + ":" + u.Art, out v)) return v;
            return new[] { u.PedVor, u.PedSeite, u.PedHoch, u.Blickrichtung };
        }

        private bool PlatzBerechnen(StationC s, Uebung u, out Vector3 pos, out float heading)
        {
            int prop;
            props.TryGetValue(s.Id, out prop);
            PlatzAmProp(prop, new Vector3(s.X, s.Y, s.Z), s.Heading, u, out pos, out heading);
            return true;
        }

        private void PlatzAmProp(int prop, Vector3 station, float stationHeading, Uebung u, out Vector3 pos, out float heading)
        {
            var v = Versatz(u);
            pos = station;
            heading = stationHeading;

            if (!string.IsNullOrEmpty(u.Ausrichtung) && prop != 0 && DoesEntityExist(prop))
            {
                var min = Vector3.Zero;
                var max = Vector3.Zero;
                GetModelDimensions((uint)GetEntityModel(prop), ref min, ref max);
                var mitte = GetOffsetFromEntityInWorldCoords(prop, (min.X + max.X) / 2f, (min.Y + max.Y) / 2f, 0f);
                var propHeading = GetEntityHeading(prop);
                var langX = (max.X - min.X) > (max.Y - min.Y);
                var laengs = u.Ausrichtung == "laengs";
                heading = propHeading + (laengs == langX ? -90f : 0f);
                pos = new Vector3(mitte.X, mitte.Y, GetEntityCoords(prop, false).Z + min.Z + 0.98f);
            }

            heading = ((heading + v[3]) % 360f + 360f) % 360f;
            var rad = heading * (float)Math.PI / 180f;
            var vorX = -(float)Math.Sin(rad);
            var vorY = (float)Math.Cos(rad);
            pos = new Vector3(pos.X + vorX * v[0] + vorY * v[1], pos.Y + vorY * v[0] - vorX * v[1], pos.Z + v[2]);
        }

        private void PlatzEinnehmen(int ped)
        {
            Vector3 pos;
            float heading;
            if (aktiv == null || uebung == null || !PlatzBerechnen(aktiv, uebung, out pos, out heading)) return;
            FreezeEntityPosition(ped, false);
            SetEntityCoordsNoOffset(ped, pos.X, pos.Y, pos.Z, false, false, false);
            SetEntityHeading(ped, heading);
            FreezeEntityPosition(ped, true);
        }

    }
}
