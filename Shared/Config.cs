using System.Collections.Generic;

namespace ToterFitness.Shared
{
    public class Uebung
    {
        public string Stat;
        public string Art;           // "frei" oder "studio"
        public string Name;
        public string Prop;          // leer = ohne Prop
        public string Spiel;         // kraft, dino, balance, yoga, rhythmus
        public string AnimDict;      // bevorzugt, Geschwindigkeit steuerbar
        public string AnimClip;
        public string Szenario;      // Ersatz, falls die Animation fehlt
        public bool Sitzend;
        public bool Route;           // Charakter joggt die gesetzte Strecke ab
        public float Blickrichtung;  // zusätzliche Drehung des Charakters in Grad (z. B. 180 = umgekehrt)
        public bool Steuerbar;       // Animation läuft nur, solange das Kraft-Spiel über der Schwelle ist
        public float RuhePhase;
        public int Dauer;            // Sekunden. Studio: 4 Übungen zusammen 10 Minuten, frei: 15 Minuten

        // Platz am Prop: "laengs" = entlang der langen Seite (Matte, Bank, Laufband), "quer" = davor (Klimmzugstange),
        // leer = Position und Blickrichtung der Station. Der Charakter steht immer auf der Mitte des Props.
        public string Ausrichtung = "";
        public float PedVor, PedSeite, PedHoch;   // Feinjustierung in Metern (vor/zurück, rechts/links, hoch/runter)

        // Treffer im Lauf-Spiel ohne Strecke (Laufband): statt Stolpern diese Animation, erst das letzte Leben wirft einen runter.
        public string TrefferDict, TrefferClip;

        // Was passiert, wenn man es verbockt: Stoß relativ zum Charakter (seitlich, vorwärts, hoch) und ein Spruch.
        public float PatzerSeite, PatzerVor, PatzerHoch;
        public string PatzerText;
    }

    public class Supplement
    {
        public string Item, Name, Beschreibung, Wirkung, Animation;
        public int Preis;
        public long Sekunden;
    }

    public static class Config
    {
        // ---------------------------------------------------------- Supplements (am Gym-Empfang zu kaufen)
        // Wirkung: "zugewinn" (mehr Trainingserfolg), "abbau" (kein Abbau), "muskelkater" (beendet Muskelkater).
        // Bilder: inventar/<item>.png nach qb-inventory/html/images.
        public static readonly List<Supplement> Supplements = new List<Supplement>
        {
            new Supplement { Item = "proteinshake", Name = "Protein-Shake", Preis = 80, Wirkung = "zugewinn", Sekunden = 2 * 3600, Animation = "trinken",
                Beschreibung = "2 Stunden lang +1 Punkt pro Übung." },
            new Supplement { Item = "vitamine", Name = "Vitamin-Kapseln", Preis = 120, Wirkung = "abbau", Sekunden = 24 * 3600, Animation = "essen",
                Beschreibung = "24 Stunden lang bauen deine Fitness-Werte nicht ab." },
            new Supplement { Item = "muskelbalsam", Name = "Muskelbalsam", Preis = 60, Wirkung = "muskelkater", Sekunden = 0, Animation = "einreiben",
                Beschreibung = "Beendet den Muskelkater sofort." }
        };
        public const int ShakePunkte = 1;          // +1 Punkt pro geschaffter Übung, solange der Shake wirkt

        // ---------------------------------------------------------- Ränge im Gym-Job (grade-Nummer aus qb-core/shared/jobs.lua)
        // 0 = Rezeption (verkauft am Empfang), ab RangTrainer = Trainer (coachen, Kurse planen),
        // ab RangChef = Chef (Preise, Team, Kurse absagen). QBCore-Boss (isboss) zählt immer als Chef.
        public const int RangTrainer = 1;
        public const int RangChef = 2;

        // ---------------------------------------------------------- Personal Trainer
        // Trainer (Gym-Job im Dienst, Rang ab RangTrainer) wählen im Alt-Menü (ox_target/qb-target) einen Spieler
        // und "Coachen". Nimmt der Spieler an, gilt der Bonus bei jeder Übung, bei der der
        // Trainer in der Nähe ist, bis der Trainer abrechnet oder verwirft. Danach rechnet der Trainer im GymDesk ab (Rechnung, Steuer geht ab, Rest ans Gym)
        // oder verwirft es (kostenlos). Rechnung: Server/Bridge.cs, RechnungStellen.
        public const int TrainerPunkte = 2;                 // +2 Punkte pro geschaffter Übung mit Trainer (nach Bezahlung)
        public const float TrainerAbstand = 8f;
        public const int TrainerBuchungMinuten = 0;         // 0 = läuft, bis der Trainer es beendet
        public const int TrainerStandardPreis = 150;
        public const int TrainerMaxPreis = 5000;
        public const double TrainingSteuer = 0.05;          // Steuer auf Personal Training und Kurse, wenn die zentrale Einstellung fehlt
        // Zentrale Steuer des Servers (eine Staatskassen-Ressource). Leer = TrainingSteuer gilt. SteuerSatzExport bekommt SteuerArt
        // und gibt den Satz zurück (5 oder 0.05 geht beides). SteuerEinzahlenExport bekommt (betrag, grund).
        public const string SteuerRessource = "";
        public const string SteuerSatzExport = "";
        public const string SteuerEinzahlenExport = "";
        public const string SteuerArt = "dienstleistung";
        public static readonly bool RechnungMitZustimmung = true; // Kunde nimmt die Rechnung an oder lehnt ab
        public const int AbrechnenStunden = 2;

        // ---------------------------------------------------------- Kurse
        // Trainer und Chefs planen im GymDesk Kurse: Übung, Plätze, Preis, Ort = Standort beim Planen. Gym-Mitarbeiter legen
        // vor Ort die Matten einzeln (erste = Kursleitung), Angemeldete stellen sich an eine Matte, die Kursleitung startet
        // alle gleichzeitig. Nach der Übung geht die Rechnung mit dem Kurspreis raus, Boni gibt es erst nach Bezahlung.
        public const float KursRadius = 25f;
        public const int KursBonus = 3;                     // Punkte für Kursteilnehmer, nach bezahlter Rechnung
        public static readonly int[] KursErinnerungen = { 30, 15 };  // Nachrichten ans Handy so viele Minuten vor Kursbeginn
        public const int KursMaxTeilnehmer = 20;
        public const float MattenMinAbstand = 0.9f;         // Mitte zu Mitte, beim Legen der Matten
        public const float MatteMaxSchraeg = 0.25f;         // Höhenunterschied unter der Matte, darüber ist der Boden zu schräg
        public const float AufbauAbstand = 30f;             // so nah muss man am Kursort sein, um Matten zu legen oder abzubauen
        public const int KurseProGym = 5;                   // gleichzeitig geplante Kurse je Gym

        // ---------------------------------------------------------- Nachrichten / Verlauf
        public const int ErinnernNachTagen = 3;             // Hinweis beim Einloggen, wenn ein Wert so lange nicht trainiert wurde
        public const int VerlaufTage = 30;                  // Zeitraum der Kurven in der App              // Reichweite der Option "Coachen" im Alt-Menü              // so lange kann der Trainer ein beendetes Coaching abrechnen

        // ---------------------------------------------------------- Muskelkater
        // Alle vier Werte an einem Tag trainiert: ab dem nächsten Tageswechsel MuskelkaterStunden lang langsamer.
        // Wer im Schnitt mindestens MuskelkaterSchutzAb hat, ist das gewohnt und bekommt keinen.
        public const int MuskelkaterSchutzAb = 75;
        public const int MuskelkaterStunden = 12;
        public const float MuskelkaterTempo = 0.88f; // Bewegungstempo mit Muskelkater

        // ---------------------------------------------------------- Bestenliste
        public static string RekordEinheit(string spiel)
        {
            switch (spiel)
            {
                case "kraft": return "Wiederholungen";
                case "dino": return "Meter";
                case "balance": return "% im Gleichgewicht";
                case "yoga": return "Punkte";
                default: return "Treffer";
            }
        }

        // Obergrenze gegen manipulierte Werte, grob nach Spiel und Dauer.
        public static int RekordMaximum(string spiel, int dauer)
        {
            switch (spiel)
            {
                case "kraft": return dauer;              // höchstens etwa eine Wiederholung pro Sekunde
                case "dino": return dauer * 60;          // Strecke
                case "balance": case "yoga": return 100;
                default: return dauer;                   // Rhythmus-Treffer
            }
        }

        public const string Ressource = "toter-fitness";
        public const string Autor = "toter";
        public const string Projektseite = "https://github.com/toter99/toter-fitness";
        public const string CoreRessource = "qb-core";   // Framework, siehe Server/Bridge.cs

        public static readonly string[] Stats = { "kraft", "ausdauer", "flexibilitaet", "kondition" };

        public static readonly Dictionary<string, string> StatNamen = new Dictionary<string, string>
        {
            { "kraft", "Kraft" },
            { "ausdauer", "Ausdauer" },
            { "flexibilitaet", "Flexibilität" },
            { "kondition", "Kondition" }
        };

        public const int StatMin = 0;
        public const int StatMax = 100;
        public const int StatStart = 50;

        public const int ZugewinnFrei = 3;
        public const int ZugewinnStudio = 6;
        public const int PreisStudio = 50;          // nur für Studio-Stationen ohne Gym
        public const string KartenItem = "gymkarte";  // Item der Mitgliedskarte (Bild: qb-inventory/html/images/gymkarte.png)
        public const int KartenPruefenSekunden = 60;

        // Gym-Empfang: Standardpreise für neue Gyms (pro Gym im Admin-Menü änderbar) und Laufzeiten.
        public const int PreisTag = 100, PreisWoche = 500, PreisMonat = 1500;
        public const int TageTag = 1, TageWoche = 7, TageMonat = 30;

        // Probetag: ein Tag ohne Mitarbeiter am Empfang kaufen, teurer als die Tageskarte, pro Spieler nur einmal je Gym.
        public const bool ProbetagAn = true;
        public const double ProbetagFaktor = 2.0;   // Preis = Tageskarte des Gyms mal Faktor
        public const int ProbetagMindestPreis = 100;
        public const bool EmpfangNurImDienst = true;   // Mitarbeiter müssen im Dienst sein
        public const float EmpfangAbstand = 1.6f;
        public const float KundenAbstand = 6f;          // so nah muss der Kunde beim Verkauf am Empfang stehen
        public const int AngebotSekunden = 60;          // so lange kann der Kunde annehmen  // so oft werden abgelaufene Karten entfernt
        public const string Konto = "bank";

        public const int AbklingzeitSekunden = 0;   // zusätzliche Pause zwischen zwei Trainings eines Werts (EinmalProTag gilt sowieso)
        public const int MindestDauerSekunden = 8;

        // Abbau pro Wert: jeder Wert für sich, ab SchonfristTage nach seinem letzten Training.
        public const int AbbauOhneMitgliedschaft = 1;  // Punkte pro Tag ohne gültige Gym-Karte
        public const int AbbauMitMitgliedschaft = 2;   // Punkte pro Tag mit gültiger Gym-Karte
        public const int SchonfristTage = 2;
        public const int AbbauUntergrenze = 20;

        // Zeitzone für Tageswechsel, Kurszeiten und Datumsanzeigen (IANA-Name, unter Windows der Windows-Name).
        public const string Zeitzone = "Europe/Berlin";
        public const string ZeitzoneWindows = "W. Europe Standard Time";

        // Jede Übung (also jeder Wert) einmal pro Tag. Neuer Tag ab dieser Uhrzeit (Config.Zeitzone).
        public const bool EinmalProTag = true;
        public const int TagesResetStunde = 4;

        public const float RoutePunktRadius = 3.5f; // Joggen: so viele Meter vor einem Streckenpunkt geht es zum nächsten

        // GTA-eigener Ausdauerwert (Ausdauerbalken beim Rennen). Bei 100 ist er in GTA fast unbegrenzt,
        // der Balken verschwindet. Deshalb wird höchstens bis hierhin gesetzt.
        public const int GtaAusdauerMax = 75;   // Kondition ab 90 -> GTA-Ausdauer 75 (der eigentliche Dauersprint kommt von VollAb)
        // Start ist 50. Zwischen NegativUnter und PositivAb passiert nichts, darunter Nachteile, darüber Vorteile.
        public const int NegativUnter = 35;
        public const int PositivAb = 65;
        public const int VollAb = 90;                // ab hier volle Wirkung jedes Werts (bei Kondition: Dauersprint)
        public const float SprintTempoPlus = 0.08f;  // Ausdauer: Sprint bis zu 8 % schneller
        public const float SchwimmTempoPlus = 0.15f; // Ausdauer: Schwimmen bis zu 15 % schneller

        // Kondition: so lange kann man am Stück sprinten, danach kurz Luft holen.
        public const float SprintMinSekunden = 8f;    // bei Kondition 0
        public const float SprintNormalSekunden = 30f; // bei Kondition 65
        public const float SprintMaxSekunden = 60f;   // knapp unter 90, ab 90 Dauersprint
        public const bool LuftholenAnimation = true;  // beim Luftholen Hände auf die Knie (nur Oberkörper)
        // Flexibilität: so viel weniger Schaden bei Stürzen ab 90 (ab 65 steigt es an). Technisch gibt die Mod nach dem Sturz
        // einen Teil der Lebenspunkte zurück. 0 = aus (Standard, damit nichts an Medic- oder Heil-Regeln vorbei heilt).
        public const float FallschadenWeniger = 0f;

        public const int PatzerUnter = 35;          // unter so vielen Prozent geht die Übung schief
        public const int BelegtMaxSekunden = 600;   // danach gilt eine Station wieder als frei (lange Laufrunden)

        // Auswirkungen der Werte auf den Charakter
        public const bool GangartAnpassen = true;      // Gangart bei sehr niedrigen bzw. hohen Werten (aus, falls ihr Walkstyles per Menü nutzt)
        public const int SchwachUnter = 25;           // darunter gilt ein Wert als schwach
        public const int StarkUeber = 85;             // darüber als stark
        public const string GangFett = "move_m@fat@a";
        public const string GangSchwach = "move_m@sad@a";
        public const string GangStark = "";   // leer: "move_m@muscle@a" macht Laufen und Rennen langsamer

        // Aussehen: bei wenig Ausdauer und Kondition volleres Gesicht und dicker Hals, bei wenig Kraft eingefallenes Gesicht.
        // Wird nur auf Freemode-Charakteren angewendet und nicht gespeichert. Wird ein Wert wieder normal, lädt das
        // Kleidungs-Skript den gespeicherten Charakter neu (qb-clothing). Bei illenium-appearance das Client-Event eintragen.
        public const bool AussehenAnpassen = true;
        public const string SkinNeuLadenServerEvent = "qb-clothing:loadPlayerSkin";
        public const string SkinNeuLadenClientEvent = "";   // z. B. "illenium-appearance:client:reloadSkin"

        public const int ChefMaxPreis = 100000;

        // Handy mit den Apps "Fitness" und "GymDesk" (Client/Telefon.cs). Spieler nutzen nur die Apps,
        // das Menü im Spiel ist das Admin-Menü (/fitnessadmin).
        public const string Telefon = "lb-phone";
        public const string AppId = "toter-fitness";
        public const string DeskAppId = "toter-gymdesk";   // App GymDesk, nur für Mitarbeiter eines Gyms

        // Körpertyp: "fett" (Ausdauer und Kondition schwach), "athletisch" (viel Kraft und guter Schnitt), sonst "normal".
        public const int AthletischKraftAb = 70;
        public const int AthletischSchnittAb = 60;

        // Kleidung aus der Ressource toter-koerper. Zahlen ab 0 = genau diese Nummer aus dem Kleidungsmenü,
        // negative Zahlen = vom Ende der Kategorie gezählt (-1 = letzter Eintrag). So lädt FiveM Addon-Kleidung,
        // solange nach toter-koerper kein anderes Kleidungspaket kommt. Passt es nicht, die Nummer aus dem Menü eintragen.
        // In toter-koerper kommt zuerst das Kollix Fat Pack (1 Oberteil, 1 Hose), danach mp25 Muscular Body (3 Oberteile, 8 Oberkörper).
        public const bool FettKleidung = false;    // dick: dicke Hose, optional Hoodie (nur MP Male)
        public const bool FettHoodie = false;      // Kollix-Hoodie: das Modell fällt in FiveM zu einem kleinen Quadrat zusammen (Körper unsichtbar), deshalb aus
        public const string FettPaket = "kollixfat";        // Teil des Paketnamens, darüber wird die Nummer gesucht
        public const string AthletischPaket = "muscularbody";
        public const int AthletischOberkoerperNr = 0;       // welcher der 8 muskulösen Oberkörper (0 bis 7)
        // Die Nummern darunter gelten nur, falls das Paket nicht über den Namen gefunden wird.
        public const int FettOberteil = -4;        // Kategorie 11 (Oberteil): Kollix-Hoodie, danach kommen noch die 3 mp25-Oberteile
        public const int FettOberteilFarbe = 0;    // 0 bis 5
        public const int FettHose = -1;            // Kategorie 4 (Hose)
        public const int FettHoseFarbe = 0;        // 0 bis 2
        public const int FettArme = 4;             // Kategorie 3 (Arme/Oberkörper) passend zum langärmligen Hoodie
        public const int FettUnterhemd = 15;       // Kategorie 8, 15 = keins

        public const bool AthletischKoerper = false; // athletisch: muskulöser Oberkörper aus einem Kleidungspaket (nicht enthalten, siehe README), nur MP Male
        public const int OhneOberteil = 15;         // Kategorie 11: so heißt "kein Oberteil" beim MP Male
        public const int AthletischOberkoerper = -8; // Kategorie 3: einer der 8 mp25-Oberkörper (-8 bis -1), im Menü ausprobieren

        // Tragen: einen "fetten" Spieler kann nur tragen, wer athletisch ist.
        public const bool FettNurAthletischTragbar = true;

        public static string KoerperTyp(int kraft, int ausdauer, int flexibilitaet, int kondition)
        {
            var schnitt = (kraft + ausdauer + flexibilitaet + kondition) / 4;
            if (ausdauer < SchwachUnter && kondition < SchwachUnter) return "fett";
            if (kraft >= AthletischKraftAb && schnitt >= AthletischSchnittAb) return "athletisch";
            return "normal";
        }

        public static bool KannTragen(string traeger, string getragener)
        {
            return !FettNurAthletischTragbar || getragener != "fett" || traeger == "athletisch";
        }

        public const float InteraktionAbstand = 1.8f;
        public const float MarkerAbstand = 25f;
        public const float PropAbstand = 150f;

        public static readonly Dictionary<string, Uebung> Uebungen = new Dictionary<string, Uebung>
        {
            { "kraft:frei", new Uebung {
                Stat = "kraft", Art = "frei", Name = "Klimmzüge", PatzerVor = -1.5f, PatzerHoch = 0.5f, PatzerText = "Deine Finger geben nach, du rutschst von der Stange.", Prop = "prop_beach_bars_02", Spiel = "kraft",
                AnimDict = "amb@prop_human_muscle_chin_ups@male@base", AnimClip = "base", Steuerbar = true,
                Szenario = "PROP_HUMAN_MUSCLE_CHIN_UPS", Ausrichtung = "quer", PedVor = -0.16f, PedHoch = 0.12f, RuhePhase = 0.429f, Dauer = 180 } }, // im Spiel justiert für prop_beach_bars_02
            { "kraft:studio", new Uebung {
                Stat = "kraft", Art = "studio", Name = "Bankdrücken", PatzerSeite = 4f, PatzerHoch = 2f, PatzerText = "Die Hantel war zu schwer, du rollst von der Bank.", Prop = "prop_muscle_bench_01", Spiel = "kraft",
                Szenario = "PROP_HUMAN_SEAT_MUSCLE_BENCH_PRESS", Sitzend = true, Ausrichtung = "laengs", PedHoch = -0.45f, Dauer = 120 } },
            { "ausdauer:frei", new Uebung {
                Stat = "ausdauer", Art = "frei", Name = "Joggen", PatzerVor = 7f, PatzerHoch = 1f, PatzerText = "Du stolperst über deine eigenen Füße.", Prop = "", Spiel = "dino",
                Szenario = "WORLD_HUMAN_JOG_STANDING", Route = true, Dauer = 270 } },
            { "ausdauer:studio", new Uebung {
                Stat = "ausdauer", Art = "studio", Name = "Laufband", PatzerVor = -16f, PatzerHoch = 4f, PatzerText = "Das Laufband war schneller als du.", Prop = "apa_p_apdlc_treadmill_s", Spiel = "dino", Blickrichtung = 180f,
                Szenario = "WORLD_HUMAN_JOG_STANDING", Ausrichtung = "laengs", PedHoch = 0.15f,
                TrefferDict = "re@construction", TrefferClip = "out_of_breath", Dauer = 180 } },
            { "flexibilitaet:frei", new Uebung {
                Stat = "flexibilitaet", Art = "frei", Name = "Yoga", PatzerSeite = 3.5f, PatzerHoch = 0.5f, PatzerText = "Du verlierst das Gleichgewicht und kippst um.", Prop = "prop_yoga_mat_01", Spiel = "yoga",
                AnimDict = "amb@world_human_yoga@male@base", AnimClip = "base_a", Szenario = "WORLD_HUMAN_YOGA", Ausrichtung = "laengs", Dauer = 240 } },
            { "flexibilitaet:studio", new Uebung {
                Stat = "flexibilitaet", Art = "studio", Name = "Pilates-Kurs", PatzerSeite = -3.5f, PatzerHoch = 0.5f, PatzerText = "Die Trainerin schaut weg, du kippst trotzdem um.", Prop = "prop_yoga_mat_03", Spiel = "balance",
                AnimDict = "amb@world_human_yoga@male@base", AnimClip = "base_b", Szenario = "WORLD_HUMAN_YOGA", Ausrichtung = "laengs", Dauer = 150 } },
            { "kondition:frei", new Uebung {
                Stat = "kondition", Art = "frei", Name = "Liegestütze", PatzerVor = 1.5f, PatzerText = "Deine Arme geben nach, Gesicht voran auf den Boden.", Prop = "", Spiel = "rhythmus",
                AnimDict = "amb@world_human_push_ups@male@base", AnimClip = "base", Szenario = "WORLD_HUMAN_PUSH_UPS", Dauer = 210 } },
            { "kondition:studio", new Uebung {
                Stat = "kondition", Art = "studio", Name = "Zirkeltraining", PatzerSeite = 2.5f, PatzerHoch = 1f, PatzerText = "Völlig aus der Puste, du klappst zusammen.", Prop = "prop_yoga_mat_02", Spiel = "rhythmus",
                AnimDict = "amb@world_human_sit_ups@male@base", AnimClip = "base", Szenario = "WORLD_HUMAN_SIT_UPS", Ausrichtung = "laengs", Dauer = 150 } },

            // Kurse: Art "kurs-..." zählt beim Zugewinn wie Studio. Matten bringt der Trainer mit und baut sie vor Ort auf.
            { "flexibilitaet:kurs-yoga", new Uebung {
                Stat = "flexibilitaet", Art = "kurs-yoga", Name = "Yoga-Kurs", PatzerSeite = 3.5f, PatzerHoch = 0.5f, PatzerText = "Du verlierst das Gleichgewicht und kippst um.", Prop = "prop_yoga_mat_01", Spiel = "yoga",
                AnimDict = "amb@world_human_yoga@male@base", AnimClip = "base_a", Szenario = "WORLD_HUMAN_YOGA", Ausrichtung = "laengs", Dauer = 240 } },
            { "flexibilitaet:kurs-pilates", new Uebung {
                Stat = "flexibilitaet", Art = "kurs-pilates", Name = "Pilates-Kurs", PatzerSeite = -3.5f, PatzerHoch = 0.5f, PatzerText = "Du kippst aus der Position.", Prop = "prop_yoga_mat_03", Spiel = "balance",
                AnimDict = "amb@world_human_yoga@male@base", AnimClip = "base_b", Szenario = "WORLD_HUMAN_YOGA", Ausrichtung = "laengs", Dauer = 150 } },
            { "kraft:kurs-hanteln", new Uebung {
                Stat = "kraft", Art = "kurs-hanteln", Name = "Hantel-Kurs", PatzerVor = -1.5f, PatzerHoch = 0.3f, PatzerText = "Die Hantel rutscht dir aus der Hand.", Prop = "prop_yoga_mat_02", Spiel = "kraft",
                Szenario = "WORLD_HUMAN_MUSCLE_FREE_WEIGHTS", Ausrichtung = "laengs", Dauer = 150 } },
            { "kondition:kurs-situps", new Uebung {
                Stat = "kondition", Art = "kurs-situps", Name = "Sit-up-Kurs", PatzerSeite = 2.5f, PatzerHoch = 1f, PatzerText = "Völlig aus der Puste, du klappst zusammen.", Prop = "prop_yoga_mat_02", Spiel = "rhythmus",
                AnimDict = "amb@world_human_sit_ups@male@base", AnimClip = "base", Szenario = "WORLD_HUMAN_SIT_UPS", Ausrichtung = "laengs", Dauer = 150 } },
            { "kondition:kurs-liegestuetze", new Uebung {
                Stat = "kondition", Art = "kurs-liegestuetze", Name = "Liegestütz-Kurs", PatzerVor = 1.5f, PatzerText = "Deine Arme geben nach, Gesicht voran auf die Matte.", Prop = "prop_yoga_mat_02", Spiel = "rhythmus",
                AnimDict = "amb@world_human_push_ups@male@base", AnimClip = "base", Szenario = "WORLD_HUMAN_PUSH_UPS", Ausrichtung = "laengs", Dauer = 150 } },
            { "ausdauer:kurs-joggen", new Uebung {
                Stat = "ausdauer", Art = "kurs-joggen", Name = "Lauf-Kurs", PatzerVor = 7f, PatzerHoch = 1f, PatzerText = "Du stolperst über deine eigenen Füße.", Prop = "", Spiel = "dino",
                Szenario = "WORLD_HUMAN_JOG_STANDING", Route = true, Dauer = 270 } }
        };

        public static bool IstKurs(string art)
        {
            return art != null && art.StartsWith("kurs-");
        }

        // Übungen, die ein Trainer als Kurs planen kann (Schlüssel aus Uebungen).
        public static readonly string[] KursArten =
        {
            "flexibilitaet:kurs-yoga", "flexibilitaet:kurs-pilates", "kraft:kurs-hanteln",
            "kondition:kurs-situps", "kondition:kurs-liegestuetze", "ausdauer:kurs-joggen"
        };

        public static Uebung UebungFuer(string stat, string art)
        {
            Uebung u;
            return Uebungen.TryGetValue(stat + ":" + art, out u) ? u : null;
        }

        // Datenbank kennt "kostenlos"/"gym", im Spiel heißt es "frei"/"studio".
        public static string ArtAusDb(string stationType)
        {
            return stationType == "gym" ? "studio" : "frei";
        }

        public static string ArtZuDb(string art)
        {
            return art == "studio" ? "gym" : "kostenlos";
        }
    }
}
