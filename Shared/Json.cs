using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ToterFitness.Shared
{
    public static class Json
    {
        public static string Schreiben(object wert)
        {
            var sb = new StringBuilder();
            Wert(sb, wert);
            return sb.ToString();
        }

        private static void Wert(StringBuilder sb, object wert)
        {
            if (wert == null) { sb.Append("null"); return; }
            if (wert is string s) { Text(sb, s); return; }
            if (wert is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (wert is float || wert is double || wert is decimal)
            {
                sb.Append(Convert.ToDouble(wert).ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }
            if (wert is int || wert is long || wert is short || wert is byte || wert is uint || wert is ulong)
            {
                sb.Append(Convert.ToInt64(wert).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (wert is IDictionary<string, object> dict)
            {
                sb.Append('{');
                var erstes = true;
                foreach (var kv in dict)
                {
                    if (!erstes) sb.Append(',');
                    erstes = false;
                    Text(sb, kv.Key);
                    sb.Append(':');
                    Wert(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (wert is IEnumerable liste)
            {
                sb.Append('[');
                var erstes = true;
                foreach (var e in liste)
                {
                    if (!erstes) sb.Append(',');
                    erstes = false;
                    Wert(sb, e);
                }
                sb.Append(']');
                return;
            }
            Text(sb, wert.ToString());
        }

        private static void Text(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
