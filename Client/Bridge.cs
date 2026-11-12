using System;
using System.Collections.Generic;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    // ======================================================================================
    //  ANSCHLÜSSE (Client)
    //  Alles, was toter-fitness auf dem Client von anderen Ressourcen braucht.
    //  Standard ist QBCore (QBCore:Notify, qb-clothing, lb-phone).
    //  Für einen anderen Server nur diese Datei anpassen (dazu Client/Telefon.cs und Client/ziel.lua).
    //  Beispiele für andere Setups: ANSCHLUESSE.md
    // ======================================================================================
    public partial class Main
    {
        // Meldung oben am Bildschirm. typ: "success", "error" oder "primary".
        private void Hinweis(string text, string typ = "primary", int dauer = 4000)
        {
            TriggerEvent("QBCore:Notify", text, typ, dauer);
        }

        // Handy zuklappen, z. B. bevor man Matten legt. lb-phone: Export ToggleOpen, sonst legt der Spieler es selbst weg.
        private void HandyZu()
        {
            try
            {
                if (GetResourceState(Config.Telefon) == "started") Exports[Config.Telefon].ToggleOpen(false);
                else Hinweis("Leg das Handy weg, dann geht es los.", "primary", 4000);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Handy schließen: {e.Message}");
            }
        }

        // Wird aufgerufen, sobald der Charakter geladen ist (nach Charakterauswahl).
        private void SpielerGeladenAnmelden(Action beimLaden)
        {
            EventHandlers["QBCore:Client:OnPlayerLoaded"] += beimLaden;
        }

        // Gespeichertes Aussehen (Gesicht, Kleidung) neu laden, nachdem toter-fitness etwas verändert hat.
        // qb-clothing: Server-Event. rcore_clothing oder illenium-appearance: deren Event in Config eintragen.
        private void SkinNeuLaden()
        {
            if (!string.IsNullOrEmpty(Config.SkinNeuLadenServerEvent)) TriggerServerEvent(Config.SkinNeuLadenServerEvent);
            if (!string.IsNullOrEmpty(Config.SkinNeuLadenClientEvent)) TriggerEvent(Config.SkinNeuLadenClientEvent);
        }

        // Wird aufgerufen, wenn sich Job oder Dienst des Charakters ändern (für die App GymDesk).
        private void JobWechselAnmelden(Action beimWechsel)
        {
            EventHandlers["QBCore:Client:OnJobUpdate"] += new Action<object>(job => beimWechsel());
            EventHandlers["QBCore:Client:SetDuty"] += new Action<object>(dienst => beimWechsel());
        }

        // Option „Coachen“ im Alt-Menü (ox_target, sonst qb-target) steht in Client/ziel.lua.
        // Hier wird nur gemeldet, ob sie gerade angezeigt werden soll.
        private void ZielStatusMelden(bool darf)
        {
            TriggerEvent("toter-fitness:client:zielStatus", darf);
        }


        // Addon-Kleidung über den Namen ihres Pakets finden (FiveM-Natives für Kleidungs-Sammlungen).
        // Gibt die Nummer für SetPedComponentVariation zurück, -1 wenn das Paket nicht geladen ist.
        private static int KleidungNummer(int ped, int kategorie, string teil, int lokal)
        {
            try
            {
                var anzahl = Function.Call<int>((Hash)0x45946359u, ped);                     // GET_PED_COLLECTIONS_COUNT
                for (var i = 0; i < anzahl; i++)
                {
                    var name = Function.Call<string>((Hash)0xFED5D83Au, ped, i);             // GET_PED_COLLECTION_NAME
                    if (name == null || name.IndexOf(teil, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var vorhanden = Function.Call<int>((Hash)0x310D0271u, ped, kategorie, name); // GET_NUMBER_OF_PED_COLLECTION_DRAWABLE_VARIATIONS
                    if (lokal >= vorhanden) return -1;
                    return Function.Call<int>((Hash)0x280F1FC3u, ped, kategorie, name, lokal); // GET_PED_DRAWABLE_GLOBAL_INDEX_FROM_COLLECTION
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Kleidungspakete: {e.Message}");
            }
            return -1;
        }

        private static List<string> KleidungPakete(int ped)
        {
            var liste = new List<string>();
            try
            {
                var anzahl = Function.Call<int>((Hash)0x45946359u, ped);
                for (var i = 0; i < anzahl; i++)
                {
                    var name = Function.Call<string>((Hash)0xFED5D83Au, ped, i);
                    liste.Add($"\"{name}\" Oberteile {Function.Call<int>((Hash)0x310D0271u, ped, 11, name)}, " +
                              $"Hosen {Function.Call<int>((Hash)0x310D0271u, ped, 4, name)}, Oberkörper {Function.Call<int>((Hash)0x310D0271u, ped, 3, name)}");
                }
            }
            catch (Exception e)
            {
                liste.Add("Abfrage nicht möglich: " + e.Message);
            }
            return liste;
        }
    }
}
