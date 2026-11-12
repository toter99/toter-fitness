using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;

namespace ToterFitness.Server
{
    public class Station
    {
        public int Id;
        public string Name;
        public string Stat;
        public string Art;
        public string Prop;
        public float X, Y, Z, Heading, Hoehe;
        public int GymId;
        public int KursId, Platz;      // nur bei aufgebauten Kurs-Matten (nicht in der Datenbank)
        public bool Leitung;
        public List<float[]> Route = new List<float[]>();

        public Dictionary<string, object> FuerClient()
        {
            var route = new List<object>();
            foreach (var p in Route) route.Add(new List<object> { p[0], p[1], p[2] });
            return new Dictionary<string, object>
            {
                { "id", Id }, { "name", Name }, { "stat", Stat }, { "art", Art }, { "prop", Prop ?? "" },
                { "x", X }, { "y", Y }, { "z", Z }, { "heading", Heading }, { "hoehe", Hoehe }, { "route", route }, { "gym", GymId },
                { "kurs", KursId }, { "platz", Platz }, { "leitung", Leitung }
            };
        }

        public string RouteText()
        {
            var teile = new List<string>();
            foreach (var p in Route)
                teile.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##},{2:0.##}", p[0], p[1], p[2]));
            return string.Join(";", teile);
        }

        public static List<float[]> RouteLesen(string text)
        {
            var route = new List<float[]>();
            if (string.IsNullOrWhiteSpace(text)) return route;
            foreach (var punkt in text.Split(';'))
            {
                var w = punkt.Split(',');
                if (w.Length != 3) continue;
                float x, y, z;
                if (float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
                    float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                    route.Add(new[] { x, y, z });
            }
            return route;
        }
    }

    public partial class Main
    {
        private async Task<object> Sql(string art, string sql, params object[] werte)
        {
            var tcs = new TaskCompletionSource<object>();
            var antwort = new Action<dynamic>(r => tcs.TrySetResult((object)r));
            var db = Exports["oxmysql"];
            switch (art)
            {
                case "query": db.query(sql, werte, antwort); break;
                case "insert": db.insert(sql, werte, antwort); break;
                default: db.update(sql, werte, antwort); break;
            }
            var fertig = await Task.WhenAny(tcs.Task, Delay(10000));
            if (fertig != tcs.Task)
            {
                Debug.WriteLine($"[{Config.Ressource}] Datenbank antwortet nicht: {sql}");
                return null;
            }
            return tcs.Task.Result;
        }

        private async Task<List<IDictionary<string, object>>> Abfrage(string sql, params object[] werte)
        {
            var zeilen = new List<IDictionary<string, object>>();
            var r = await Sql("query", sql, werte);
            if (r is IEnumerable<object> liste)
                foreach (var z in liste)
                    if (z is IDictionary<string, object> d) zeilen.Add(d);
            return zeilen;
        }

        private static int Zahl(IDictionary<string, object> z, string feld, int sonst = 0)
        {
            object w;
            if (z == null || !z.TryGetValue(feld, out w) || w == null) return sonst;
            try { return Convert.ToInt32(w); } catch { return sonst; }
        }

        private static long GrosseZahl(IDictionary<string, object> z, string feld)
        {
            object w;
            if (z == null || !z.TryGetValue(feld, out w) || w == null) return 0;
            try { return Convert.ToInt64(w); } catch { return 0; }
        }

        private static float Komma(IDictionary<string, object> z, string feld)
        {
            object w;
            if (z == null || !z.TryGetValue(feld, out w) || w == null) return 0f;
            try { return Convert.ToSingle(w, CultureInfo.InvariantCulture); } catch { return 0f; }
        }

        private static string Text(IDictionary<string, object> z, string feld)
        {
            object w;
            return z != null && z.TryGetValue(feld, out w) && w != null ? w.ToString() : "";
        }

        private static long Jetzt()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
