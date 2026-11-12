using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using ToterFitness.Shared;

namespace ToterFitness.Server
{
    public partial class Main
    {
        private class Angebot
        {
            public string Art;
            public int GymId;
            public string Tarif, Item;
            public int Preis;
            public string Verkaeufer;
            public long Bis;
            public Buchung Buchung;
        }

        private readonly Dictionary<string, Angebot> angebote = new Dictionary<string, Angebot>();

        private static readonly string[] Tarife = { "tag", "woche", "monat" };

        private static string TarifName(string t)
        {
            return t == "tag" ? $"{Config.TageTag} Tag" : t == "woche" ? $"{Config.TageWoche} Tage" : $"{Config.TageMonat} Tage";
        }

        private static int TarifTage(string t)
        {
            return t == "tag" ? Config.TageTag : t == "woche" ? Config.TageWoche : Config.TageMonat;
        }

        private static int TarifPreis(Gym g, string t)
        {
            return t == "tag" ? g.PreisTag : t == "woche" ? g.PreisWoche : g.PreisMonat;
        }

        private bool AmEmpfang(Player player, Gym g)
        {
            if (g?.Empfang == null) return false;
            var p = Position(player);
            return Abstand(p[0], p[1], p[2], g.Empfang[0], g.Empfang[1], g.Empfang[2]) < Config.EmpfangAbstand + 2f;
        }

        private bool IstMitarbeiter(Player player, Gym g)
        {
            if (string.IsNullOrEmpty(g?.Job)) return false;
            var job = JobVon(player);
            return job.Name == g.Job && (!Config.EmpfangNurImDienst || job.ImDienst);
        }

        private bool Nah(Player a, Player b, float abstand)
        {
            var p = Position(a);
            var k = Position(b);
            return Abstand(p[0], p[1], p[2], k[0], k[1], k[2]) <= abstand;
        }

        private List<object> SpielerInDerNaehe(Player player, float abstand)
        {
            return Players.ToList()
                .Where(x => x.Handle != player.Handle && CitizenId(x) != null && Nah(player, x, abstand))
                .Select(x => (object)new Dictionary<string, object> { { "id", int.Parse(x.Handle) }, { "name", Charname(x) } })
                .ToList();
        }

        private static int ProbetagPreis(Gym g)
        {
            return Math.Max(Config.ProbetagMindestPreis, (int)Math.Round(g.PreisTag * Config.ProbetagFaktor));
        }

        private async Task<bool> ProbetagGenutzt(string cid, int gymId)
        {
            return (await Abfrage("SELECT 1 FROM fitness_probetage WHERE citizenid = ? AND gym_id = ? LIMIT 1", cid, gymId)).Count > 0;
        }

        [EventHandler("toter-fitness:server:empfang")]
        private async void OnEmpfang([FromSource] Player player, int gymId)
        {
            if (Gedrosselt(player, "OnEmpfang", 500)) return;
            var g = gyms.FirstOrDefault(x => x.Id == gymId);
            if (g == null || !AmEmpfang(player, g)) return;

            string modus;
            if (IstMitarbeiter(player, g)) modus = "verkauf";
            else if (string.IsNullOrEmpty(g.Job)) modus = "kauf";
            else modus = "info";

            var daten = new Dictionary<string, object>
            {
                { "gymId", g.Id },
                { "gym", g.Name },
                { "modus", modus },
                { "tarife", Tarife.Select(t => (object)new Dictionary<string, object> { { "id", t }, { "name", TarifName(t) }, { "preis", TarifPreis(g, t) } }).ToList() },
                { "shop", Config.Supplements.Where(s => g.ShopPreis(s) > 0).Select(s => (object)new Dictionary<string, object>
                    { { "item", s.Item }, { "name", s.Name }, { "preis", g.ShopPreis(s) }, { "text", s.Beschreibung } }).ToList() }
            };

            var cid = CitizenId(player);
            var eigene = KartenVon(player).Where(k => k.GymId == g.Id && k.CitizenId == cid && k.Ablauf > Jetzt()).OrderByDescending(k => k.Ablauf).FirstOrDefault();
            if (eigene != null) daten["meineKarte"] = Datum(eigene.Ablauf);
            if (modus == "verkauf") daten["kunden"] = SpielerInDerNaehe(player, Config.KundenAbstand);
            if (Config.ProbetagAn && modus == "info" && eigene == null && cid != null && !await ProbetagGenutzt(cid, g.Id))
                daten["probetag"] = ProbetagPreis(g);

            TriggerClientEvent(player, "toter-fitness:client:empfang", daten);
        }

        [EventHandler("toter-fitness:server:empfangAnbieten")]
        private void OnAnbieten([FromSource] Player player, int gymId, int kundeId, string art, string was)
        {
            if (Gedrosselt(player, "OnAnbieten", 1000)) return;
            var g = gyms.FirstOrDefault(x => x.Id == gymId);
            var kunde = SpielerVon(kundeId);
            if (g == null || kunde == null || kunde.Handle == player.Handle) return;
            if (!IstMitarbeiter(player, g) || !AmEmpfang(player, g))
            {
                Melden(player, "Verkaufen geht nur im Dienst am Empfang.", "error");
                return;
            }
            if (!Nah(player, kunde, Config.KundenAbstand))
            {
                Melden(player, "Der Kunde steht zu weit weg.", "error");
                return;
            }

            var a = new Angebot { Art = art, GymId = g.Id, Verkaeufer = player.Handle, Bis = Jetzt() + Config.AngebotSekunden };
            string text;
            if (art == "mitgliedschaft" && Tarife.Contains(was))
            {
                a.Tarif = was;
                a.Preis = TarifPreis(g, was);
                text = $"Mitgliedschaft {TarifName(was)}";
            }
            else if (art == "item")
            {
                var s = Config.Supplements.FirstOrDefault(x => x.Item == was);
                if (s == null || g.ShopPreis(s) <= 0) return;
                a.Item = s.Item;
                a.Preis = g.ShopPreis(s);
                text = s.Name;
            }
            else return;

            AngebotSchicken(player, kunde, a, g.Name, text);
        }

        private void AngebotSchicken(Player von, Player kunde, Angebot a, string titel, string text)
        {
            angebote[kunde.Handle] = a;
            TriggerClientEvent(kunde, "toter-fitness:client:angebot", new Dictionary<string, object>
            {
                { "titel", titel }, { "text", text }, { "preis", a.Preis }, { "verkaeufer", Charname(von) },
                { "sekunden", Config.AngebotSekunden }, { "art", a.Art }
            });
            Melden(von, $"Angebot an {Charname(kunde)}: {text} für ${a.Preis}.", "primary");
        }

        [EventHandler("toter-fitness:server:angebotAntwort")]
        private void OnAngebotAntwort([FromSource] Player kunde, bool ja)
        {
            if (Gedrosselt(kunde, "OnAngebotAntwort", 500)) return;
            Angebot a;
            if (!angebote.TryGetValue(kunde.Handle, out a)) return;
            angebote.Remove(kunde.Handle);
            var verkaeufer = SpielerVon(int.Parse(a.Verkaeufer));
            if (a.Bis < Jetzt())
            {
                Melden(kunde, "Das Angebot ist nicht mehr gültig.", "error");
                return;
            }
            if (!ja)
            {
                if (verkaeufer != null) Melden(verkaeufer, $"{Charname(kunde)} hat abgelehnt.", "error");
                if (a.Art == "rechnung" && a.Buchung != null && a.Buchung.Bonus.Count > 0)
                    Melden(kunde, a.Buchung.KursId > 0 ? "Rechnung abgelehnt, Trainer- und Kurs-Bonus entfallen." : "Rechnung abgelehnt, der Trainer-Bonus entfällt.", "primary");
                return;
            }
            if (a.Art == "coaching")
            {
                CoachingStarten(kunde, verkaeufer, a);
                return;
            }
            if (a.Art == "rechnung")
            {
                RechnungBezahlt(kunde, verkaeufer, a.Preis, a.Item, a.Tarif, a.Buchung);
                return;
            }
            var g = gyms.FirstOrDefault(x => x.Id == a.GymId);
            if (g == null) return;
            if (a.Art == "mitgliedschaft") Verkaufen(kunde, verkaeufer, g, a.Tarif, a.Preis);
            else if (a.Art == "item") ItemVerkaufen(kunde, verkaeufer, g, a.Item, a.Preis);
        }

        [EventHandler("toter-fitness:server:empfangKaufen")]
        private void OnSelbstKaufen([FromSource] Player kunde, int gymId, string art, string was)
        {
            if (Gedrosselt(kunde, "OnSelbstKaufen", 1000)) return;
            var g = gyms.FirstOrDefault(x => x.Id == gymId);
            if (art == "probetag")
            {
                ProbetagKaufen(kunde, g);
                return;
            }
            if (g == null || !string.IsNullOrEmpty(g.Job) || !AmEmpfang(kunde, g)) return;
            if (art == "mitgliedschaft" && Tarife.Contains(was)) Verkaufen(kunde, null, g, was, TarifPreis(g, was));
            else if (art == "item")
            {
                var s = Config.Supplements.FirstOrDefault(x => x.Item == was);
                if (s != null && g.ShopPreis(s) > 0) ItemVerkaufen(kunde, null, g, s.Item, g.ShopPreis(s));
            }
        }

        // Probetag: Platz in der Tabelle zuerst reservieren (INSERT IGNORE), so geht er auch bei doppeltem Klick nur einmal.
        private async void ProbetagKaufen(Player kunde, Gym g)
        {
            try
            {
                if (!Config.ProbetagAn || g == null || !AmEmpfang(kunde, g)) return;
                var cid = CitizenId(kunde);
                if (cid == null) return;
                if (KartenVon(kunde).Any(k => k.GymId == g.Id && k.CitizenId == cid && k.Ablauf > Jetzt()))
                {
                    Melden(kunde, "Du hast hier schon eine gültige Mitgliedschaft.", "error");
                    return;
                }
                var neu = Convert.ToInt32(await Sql("update", "INSERT IGNORE INTO fitness_probetage (citizenid, gym_id, zeit) VALUES (?, ?, ?)", cid, g.Id, Jetzt()) ?? 0);
                if (neu == 0)
                {
                    Melden(kunde, $"Deinen Probetag bei {g.Name} hattest du schon.", "error");
                    return;
                }
                var preis = ProbetagPreis(g);
                if (!Bezahlen(kunde, preis, $"Probetag {g.Name}"))
                {
                    await Sql("update", "DELETE FROM fitness_probetage WHERE citizenid = ? AND gym_id = ?", cid, g.Id);
                    Melden(kunde, $"Dafür reicht dein Konto nicht (${preis}).", "error");
                    return;
                }
                if (!KarteAusstellen(kunde, int.Parse(kunde.Handle), g.Id, Config.TageTag * 86400L))
                {
                    await Sql("update", "DELETE FROM fitness_probetage WHERE citizenid = ? AND gym_id = ?", cid, g.Id);
                    Erstatten(kunde, preis);
                    return;
                }
                AufFirmenkonto(g.Job, preis, $"Probetag {g.Name}");
                Melden(kunde, $"Probetag bei {g.Name} für ${preis}. Die Karte gilt einen Tag.", "success");
                Debug.WriteLine($"[{Config.Ressource}] Probetag {g.Name} für ${preis} an {kunde.Name}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Probetag: {e.Message}");
            }
        }

        private void Verkaufen(Player kunde, Player verkaeufer, Gym g, string tarif, int preis)
        {
            if (!Bezahlen(kunde, preis, $"Mitgliedschaft {g.Name}"))
            {
                Melden(kunde, $"Dafür reicht dein Konto nicht (${preis}).", "error");
                if (verkaeufer != null) Melden(verkaeufer, $"{Charname(kunde)} kann nicht bezahlen.", "error");
                return;
            }
            if (!KarteAusstellen(verkaeufer ?? kunde, int.Parse(kunde.Handle), g.Id, TarifTage(tarif) * 86400L))
            {
                Erstatten(kunde, preis);
                return;
            }
            AufFirmenkonto(g.Job, preis, $"Mitgliedschaft {g.Name}");
            Debug.WriteLine($"[{Config.Ressource}] Mitgliedschaft {g.Name} {TarifName(tarif)} für ${preis} an {kunde.Name}" + (verkaeufer != null ? $" von {verkaeufer.Name}" : ""));
        }

        private void ItemVerkaufen(Player kunde, Player verkaeufer, Gym g, string itemName, int preis)
        {
            var s = Config.Supplements.FirstOrDefault(x => x.Item == itemName);
            if (s == null) return;
            if (!Bezahlen(kunde, preis, s.Name))
            {
                Melden(kunde, $"Dafür reicht dein Konto nicht (${preis}).", "error");
                if (verkaeufer != null) Melden(verkaeufer, $"{Charname(kunde)} kann nicht bezahlen.", "error");
                return;
            }
            if (!ItemGeben(kunde, s.Item, 1, null))
            {
                Erstatten(kunde, preis);
                Melden(kunde, "Dein Inventar ist voll.", "error");
                if (verkaeufer != null) Melden(verkaeufer, $"{Charname(kunde)} hat keinen Platz im Inventar.", "error");
                return;
            }
            AufFirmenkonto(g.Job, preis, $"{s.Name} {g.Name}");
            Melden(kunde, $"{s.Name} für ${preis} gekauft.", "success");
            if (verkaeufer != null) Melden(verkaeufer, $"{s.Name} an {Charname(kunde)} verkauft.", "success");
        }
    }
}
