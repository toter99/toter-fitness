using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using ToterFitness.Shared;

namespace ToterFitness.Server
{
    public partial class Main
    {
        private List<Gym> ChefVon(Player player)
        {
            var job = JobVon(player);
            if (!job.Chef || job.Name == "") return new List<Gym>();
            return gyms.Where(g => g.Job == job.Name).ToList();
        }

        // Gym, in dem der Spieler arbeitet (Job des Gyms), für die App GymDesk. null = kein Mitarbeiter.
        private Dictionary<string, object> StudioVon(Player player)
        {
            var job = JobVon(player);
            if (job.Name == "") return null;
            var g = gyms.FirstOrDefault(x => !string.IsNullOrEmpty(x.Job) && x.Job == job.Name);
            if (g == null) return null;
            return new Dictionary<string, object>
            {
                { "gymId", g.Id }, { "gym", g.Name }, { "rang", job.RangName }, { "dienst", job.ImDienst },
                { "chef", job.Chef }, { "trainer", IstTrainer(player) },
                { "kurse", job.Rang >= Config.RangTrainer || job.Chef }
            };
        }

        private List<object> ChefDaten(Player player)
        {
            var eigene = ChefVon(player);
            if (eigene.Count == 0) return new List<object>();
            var leute = Players.ToList().Select(p => new { p, job = JobVon(p) }).Where(x => x.job.Name != "").ToList();
            return eigene.Select(g => (object)new Dictionary<string, object>
            {
                { "id", g.Id }, { "name", g.Name },
                { "tag", g.PreisTag }, { "woche", g.PreisWoche }, { "monat", g.PreisMonat },
                { "shop", Config.Supplements.Select(s => (object)new Dictionary<string, object>
                    { { "item", s.Item }, { "name", s.Name }, { "preis", g.ShopPreis(s) } }).ToList() },
                { "mitarbeiter", leute.Where(x => x.job.Name == g.Job).Select(x => (object)new Dictionary<string, object>
                    { { "name", Charname(x.p) }, { "rang", x.job.RangName }, { "dienst", x.job.ImDienst } }).ToList() },
                { "empfang", g.Empfang != null }
            }).ToList();
        }

        [EventHandler("toter-fitness:server:chefAktion")]
        private async void OnChefAktion([FromSource] Player player, string aktion, object datenRoh)
        {
            if (Gedrosselt(player, "OnChefAktion", 1000)) return;
            var d = AlsTabelle(datenRoh) ?? new Dictionary<string, object>();
            var g = ChefVon(player).FirstOrDefault(x => x.Id == Zahl(d, "id"));
            if (g == null)
            {
                Melden(player, "Das darf nur der Chef dieses Gyms.", "error");
                return;
            }
            try
            {
                if (aktion != "preise") return;
                Func<string, int> preis = k => Math.Max(0, Math.Min(Config.ChefMaxPreis, Zahl(d, k)));
                object shopRoh;
                d.TryGetValue("shop", out shopRoh);
                var neu = AlsTabelle(shopRoh);
                var shop = string.Join(";", Config.Supplements.Select(s =>
                    s.Item + "=" + (neu != null && neu.ContainsKey(s.Item) ? Math.Max(0, Math.Min(Config.ChefMaxPreis, Zahl(neu, s.Item))) : g.ShopPreis(s))));
                await Sql("update", "UPDATE fitness_gyms SET preis_tag = ?, preis_woche = ?, preis_monat = ?, shop = ? WHERE id = ?",
                    preis("tag"), preis("woche"), preis("monat"), shop, g.Id);
                await GymsLaden(true);
                Melden(player, $"Preise von {g.Name} gespeichert.", "success");
                Debug.WriteLine($"[{Config.Ressource}] {player.Name} hat die Preise von {g.Name} geändert.");

                var cid = CitizenId(player);
                if (cid != null) StatsSenden(player, await WerteHolen(cid, player), false, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Chef-Aktion {aktion}: {e.Message}");
            }
        }
    }
}
