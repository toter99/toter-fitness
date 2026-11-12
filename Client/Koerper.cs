using System;
using System.Collections.Generic;
using CitizenFX.Core;
using ToterFitness.Shared;
using static CitizenFX.Core.Native.API;

namespace ToterFitness.Client
{
    public partial class Main
    {
        private bool fettKleidungAn;

        private string MeinKoerper()
        {
            return Config.KoerperTyp(Wert("kraft"), Wert("ausdauer"), Wert("flexibilitaet"), Wert("kondition"));
        }

        private void KoerperExportsAnmelden()
        {
            Exports.Add("KannTragen", new Func<int, bool>(zielServerId => Config.KannTragen(MeinKoerper(), KoerperVonSpieler(zielServerId))));
            Exports.Add("Koerper", new Func<string>(MeinKoerper));
        }

        private string KoerperVonSpieler(int serverId)
        {
            try
            {
                var index = GetPlayerFromServerId(serverId);
                if (index < 0) return "normal";
                object typ = new Player(index).State.Get("toterKoerper");
                return typ as string ?? "normal";
            }
            catch
            {
                return "normal";
            }
        }

        private bool athletischAn;
        private int armeVorher = -1, armeFarbeVorher;

        private int Nummer(int ped, int kategorie, string paket, int lokal, int ersatz)
        {
            var n = KleidungNummer(ped, kategorie, paket, lokal);
            if (n >= 0) return n;
            return ersatz >= 0 ? ersatz : GetNumberOfPedDrawableVariations(ped, kategorie) + ersatz;
        }

        private List<string> KleidungInfo()
        {
            var ped = PlayerPedId();
            var zeilen = new List<string>();
            var i = 0;
            foreach (var zeile in KleidungPakete(ped)) zeilen.Add($"Paket {i++}: {zeile}");
            zeilen.Add($"Jetzt an: Oberteil {GetPedDrawableVariation(ped, 11)}, Hose {GetPedDrawableVariation(ped, 4)}, Oberkörper {GetPedDrawableVariation(ped, 3)}");
            zeilen.Add($"Muskeln: {KleidungNummer(ped, 3, Config.AthletischPaket, Config.AthletischOberkoerperNr)}");
            return zeilen;
        }

        private bool kleidungFehltGemeldet;

        private bool Gueltig(int ped, int kategorie, int nummer, int farbe)
        {
            if (nummer >= 0 && IsPedComponentVariationValid(ped, kategorie, nummer, farbe)) return true;
            if (!kleidungFehltGemeldet)
            {
                kleidungFehltGemeldet = true;
                Debug.WriteLine($"[{Config.Ressource}] Kleidung {kategorie}/{nummer} gibt es nicht. Läuft toter-koerper? Admin-Bereich, Diagnose, Kleidung zeigt die Pakete.");
            }
            return false;
        }

        private void FettKleidung()
        {
            var ped = PlayerPedId();
            if ((int)GetEntityModel(ped) != Game.GenerateHash("mp_m_freemode_01")) return;
            var typ = MeinKoerper();

            if (Config.FettKleidung && typ == "fett")
            {
                var hoodie = Config.FettHoodie;
                var oberteil = hoodie ? Nummer(ped, 11, Config.FettPaket, 0, Config.FettOberteil) : -1;
                var hose = Nummer(ped, 4, Config.FettPaket, 0, Config.FettHose);
                if ((hoodie && !Gueltig(ped, 11, oberteil, Config.FettOberteilFarbe)) || !Gueltig(ped, 4, hose, Config.FettHoseFarbe))
                {
                    if (fettKleidungAn) { fettKleidungAn = false; SkinNeuLaden(); }
                    return;
                }
                if (GetPedDrawableVariation(ped, 4) != hose || (hoodie && GetPedDrawableVariation(ped, 11) != oberteil))
                {
                    SetPedComponentVariation(ped, 4, hose, Config.FettHoseFarbe, 0);
                    if (hoodie)
                    {
                        SetPedComponentVariation(ped, 11, oberteil, Config.FettOberteilFarbe, 0);
                        SetPedComponentVariation(ped, 3, Config.FettArme, 0, 0);
                        SetPedComponentVariation(ped, 8, Config.FettUnterhemd, 0, 0);
                    }
                }
                fettKleidungAn = true;
            }
            else if (fettKleidungAn)
            {
                fettKleidungAn = false;
                SkinNeuLaden();
                return;
            }

            var muskeln = Config.AthletischKoerper && typ == "athletisch" && GetPedDrawableVariation(ped, 11) == Config.OhneOberteil;
            var oberkoerper = Nummer(ped, 3, Config.AthletischPaket, Config.AthletischOberkoerperNr, Config.AthletischOberkoerper);
            if (muskeln && !Gueltig(ped, 3, oberkoerper, 0)) muskeln = false;
            if (muskeln)
            {
                var jetzt = GetPedDrawableVariation(ped, 3);
                if (jetzt != oberkoerper)
                {
                    armeVorher = jetzt;
                    armeFarbeVorher = GetPedTextureVariation(ped, 3);
                    SetPedComponentVariation(ped, 3, oberkoerper, 0, 0);
                }
                athletischAn = true;
            }
            else if (athletischAn)
            {
                athletischAn = false;
                if (armeVorher >= 0 && GetPedDrawableVariation(ped, 3) == oberkoerper)
                    SetPedComponentVariation(ped, 3, armeVorher, armeFarbeVorher, 0);
                armeVorher = -1;
            }
        }

    }
}
