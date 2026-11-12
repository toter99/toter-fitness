using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Server
{
    // ======================================================================================
    //  ANSCHLÜSSE (Server)
    //  Alles, was toter-fitness von anderen Ressourcen braucht, läuft über diese Datei.
    //  Standard ist QBCore (qb-core, qb-banking, qb-management, qb-inventory).
    //  Für einen anderen Server nur die Funktionen hier anpassen, der Rest bleibt gleich.
    //  Jede Funktion beschreibt, was sie liefern muss. Beispiele für andere Setups: ANSCHLUESSE.md
    // ======================================================================================
    public partial class Main
    {
        private dynamic core;

        private dynamic Core()
        {
            if (core == null) core = Exports[Config.CoreRessource].GetCoreObject();
            return core;
        }

        // Spielerobjekt des Frameworks oder null.
        private dynamic QbSpieler(Player player)
        {
            if (player == null) return null;
            try { return Core().Functions.GetPlayer(int.Parse(player.Handle)); }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Spieler nicht lesbar: {e.Message}");
                return null;
            }
        }

        // Eindeutige ID des Charakters (nicht des Accounts). null, wenn kein Charakter geladen ist.
        private string CitizenId(Player player)
        {
            var xp = QbSpieler(player);
            if (xp == null) return null;
            try { return (string)xp.PlayerData.citizenid; }
            catch { return null; }
        }

        // Vor- und Nachname des Charakters, sonst der FiveM-Name.
        private string Charname(Player player)
        {
            try
            {
                var xp = QbSpieler(player);
                return $"{(string)xp.PlayerData.charinfo.firstname} {(string)xp.PlayerData.charinfo.lastname}";
            }
            catch { return player?.Name ?? ""; }
        }

        // Geburtsdatum des Charakters als Text, leer wenn unbekannt.
        private string Geburtstag(Player player)
        {
            try { return Convert.ToString(QbSpieler(player).PlayerData.charinfo.birthdate); }
            catch { return ""; }
        }

        // Job des Charakters: Name (z. B. "gym"), Rang als Zahl, Rang-Name, ob er im Dienst ist und ob er Chef ist.
        private class JobInfo
        {
            public string Name = "", RangName = "";
            public int Rang;
            public bool ImDienst, Chef;
        }

        private JobInfo JobVon(Player player)
        {
            var info = new JobInfo();
            try
            {
                var job = QbSpieler(player)?.PlayerData.job;
                if (job == null) return info;
                info.Name = (string)job.name ?? "";
                object dienst = job.onduty;
                info.ImDienst = dienst is bool b && b;
                try
                {
                    info.Rang = Convert.ToInt32(job.grade.level);
                    info.RangName = Convert.ToString(job.grade.name);
                }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Rang lesen: {e.Message}"); }
                try
                {
                    object boss = job.isboss;
                    info.Chef = boss is bool bb && bb;
                }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Boss-Status lesen: {e.Message}"); }
                if (!info.Chef) info.Chef = info.Rang >= Config.RangChef;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Job lesen: {e.Message}");
            }
            return info;
        }

        // Meldung oben am Bildschirm. typ: "success", "error" oder "primary".
        private void Melden(Player player, string text, string typ = "primary")
        {
            TriggerClientEvent(player, "QBCore:Notify", text, typ, 5000);
        }

        // ---------------------------------------------------------------- Geld

        // Betrag vom Konto (Config.Konto, Standard "bank") abziehen. true, wenn es geklappt hat.
        private bool Bezahlen(Player player, int betrag, string grund = "fitness")
        {
            if (betrag <= 0) return true;
            try
            {
                var xp = QbSpieler(player);
                if (xp == null) return false;
                object ok = xp.Functions.RemoveMoney(Config.Konto, betrag, grund);
                return ok is bool b && b;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Bezahlen: {e.Message}");
                return false;
            }
        }

        // Geld zurück aufs Konto, wenn nach dem Bezahlen etwas schiefging.
        private void Erstatten(Player player, int betrag, string grund = "fitness-erstattung")
        {
            try { QbSpieler(player)?.Functions.AddMoney(Config.Konto, betrag, grund); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Erstatten: {e.Message}"); }
        }

        // Einnahmen aufs Firmenkonto eines Jobs buchen (Mitgliedschaften, Supplements, Personal Training).
        private void AufFirmenkonto(string job, int betrag, string grund)
        {
            if (string.IsNullOrEmpty(job) || betrag <= 0) return;
            try
            {
                if (GetResourceState("qb-banking") == "started")
                {
                    Exports["qb-banking"].AddMoney(job, betrag, grund);
                    return;
                }
                if (GetResourceState("qb-management") == "started")
                {
                    Exports["qb-management"].AddMoney(job, betrag);
                    return;
                }
                Debug.WriteLine($"[{Config.Ressource}] Kein Firmenkonto gefunden, ${betrag} für {job} nicht gebucht.");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Firmenkonto {job}: {e.Message}");
            }
        }

        // ---------------------------------------------------------------- Rechnung

        // Rechnung für Personal Training. Der Kunde hat sie vorher angenommen (Config.RechnungMitZustimmung).
        // Standard: Betrag sofort vom Konto des Kunden, nach Abzug der Steuer (Steuersatz) aufs Firmenkonto.
        // Mit einem Rechnungssystem (z. B. okokBilling, jim-payments) hier stattdessen eine Rechnung erstellen, true zurückgeben
        // und Config.RechnungMitZustimmung auf false stellen, weil der Kunde dort selbst annimmt.
        private bool RechnungStellen(Player von, Player an, int betrag, string grund, string job)
        {
            if (!Bezahlen(an, betrag, grund)) return false;
            var steuer = (int)Math.Round(betrag * Steuersatz());
            AufFirmenkonto(job, betrag - steuer, grund);
            SteuerAbfuehren(steuer, grund);
            return true;
        }

        // Steuersatz als Anteil (0.05 = 5 %). Mit Config.SteuerRessource + SteuerSatzExport aus der zentralen Einstellung
        // des Servers, sonst Config.TrainingSteuer.
        private double Steuersatz()
        {
            if (Config.SteuerRessource != "" && Config.SteuerSatzExport != "" && GetResourceState(Config.SteuerRessource) == "started")
            {
                try
                {
                    var roh = Convert.ToDouble(Exports[Config.SteuerRessource][Config.SteuerSatzExport](Config.SteuerArt));
                    if (roh > 1) roh /= 100.0;
                    if (roh >= 0 && roh <= 1) return roh;
                }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Steuersatz von {Config.SteuerRessource}: {e.Message}"); }
            }
            return Config.TrainingSteuer;
        }

        // Steueranteil einer Einnahme. Mit Config.SteuerRessource + SteuerEinzahlenExport in die Staatskasse,
        // sonst nur ins Log (das Geld verlässt das Spiel).
        private void SteuerAbfuehren(int betrag, string grund)
        {
            if (betrag <= 0) return;
            if (Config.SteuerRessource != "" && Config.SteuerEinzahlenExport != "" && GetResourceState(Config.SteuerRessource) == "started")
            {
                try
                {
                    Exports[Config.SteuerRessource][Config.SteuerEinzahlenExport](betrag, grund);
                    return;
                }
                catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Steuer einzahlen bei {Config.SteuerRessource}: {e.Message}"); }
            }
            Debug.WriteLine($"[{Config.Ressource}] Steuer ${betrag} ({grund})");
        }

        // Nachricht aufs Handy (App Fitness). lb-phone: Server-Export SendNotification, qb-phone: dessen Handy-Meldung,
        // sonst normale Meldung am Bildschirm.
        private void HandyNachricht(Player player, string titel, string text)
        {
            if (player == null) return;
            try
            {
                if (GetResourceState(Config.Telefon) == "started")
                {
                    Exports[Config.Telefon].SendNotification(int.Parse(player.Handle), new Dictionary<string, object>
                    {
                        { "app", Config.AppId }, { "title", titel }, { "content", text }
                    });
                    return;
                }
                if (GetResourceState("qb-phone") == "started")
                {
                    TriggerClientEvent(player, "qb-phone:client:CustomNotification", titel, text, "fas fa-heartbeat", "#ff4f6d", 7000);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Handy-Nachricht: {e.Message}");
            }
            Melden(player, $"{titel}: {text}", "primary");
        }

        // ---------------------------------------------------------------- Inventar

        // Item beim Framework bekannt machen. Gibt es das Item schon (items.lua), passiert nichts.
        private void ItemAnlegen(string name, string label, int gewicht, bool einzeln, string beschreibung)
        {
            try
            {
                Core().Functions.AddItem(name, new Dictionary<string, object>
                {
                    { "name", name }, { "label", label }, { "weight", gewicht }, { "type", "item" },
                    { "image", name + ".png" }, { "unique", einzeln }, { "useable", true }, { "shouldClose", true },
                    { "combinable", null }, { "description", beschreibung }
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Item {name} nicht angelegt, bitte in die Item-Liste des Inventars eintragen: {e.Message}");
            }
        }

        // Aufruf, wenn ein Spieler das Item im Inventar benutzt: (Server-ID, Item-Tabelle mit slot und info).
        private void ItemBenutzbar(string name, Action<int, object> beimBenutzen)
        {
            try { Core().Functions.CreateUseableItem(name, beimBenutzen); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] {name} benutzbar machen: {e.Message}"); }
        }

        // Item mit Daten (info/metadata) geben. true, wenn es im Inventar gelandet ist.
        private bool ItemGeben(Player player, string name, int menge, Dictionary<string, object> info)
        {
            try
            {
                object ok = QbSpieler(player)?.Functions.AddItem(name, menge, false, info);
                if (!(ok is bool b && b)) return false;
                ItemAnzeige(player, name, "add");
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Item geben {name}: {e.Message}");
                return false;
            }
        }

        // Item entfernen, mit slot genau aus diesem Platz (null = irgendeins).
        private bool ItemNehmen(Player player, string name, int menge, object slot)
        {
            try
            {
                object ok = QbSpieler(player)?.Functions.RemoveItem(name, menge, slot);
                if (!(ok is bool b && b)) return false;
                ItemAnzeige(player, name, "remove");
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Item nehmen {name}: {e.Message}");
                return false;
            }
        }

        // Alle Items im Inventar, je eine Tabelle mit name, slot und info.
        private List<IDictionary<string, object>> ItemsVon(Player player)
        {
            var liste = new List<IDictionary<string, object>>();
            object items;
            try { items = QbSpieler(player)?.PlayerData.items; }
            catch { return liste; }
            IEnumerable eintraege = null;
            if (items is IDictionary<string, object> d) eintraege = d.Values;
            else if (items is IDictionary nd) eintraege = nd.Values;
            else if (items is IEnumerable e) eintraege = e;
            if (eintraege == null) return liste;
            foreach (var roh in eintraege)
                if (roh is IDictionary<string, object> item) liste.Add(item);
            return liste;
        }

        // Kleine Einblendung am Bildschirmrand "hinzugefügt/entfernt".
        private void ItemAnzeige(Player player, string name, string art)
        {
            object daten = null;
            try
            {
                var items = (object)Core().Shared.Items;
                if (items is IDictionary<string, object> d && d.ContainsKey(name)) daten = d[name];
            }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Item-Daten lesen: {e.Message}"); }
            if (daten == null) daten = new Dictionary<string, object> { { "name", name }, { "label", name }, { "image", name + ".png" } };
            TriggerClientEvent(player, "inventory:client:ItemBox", daten, art);
        }
    }
}
