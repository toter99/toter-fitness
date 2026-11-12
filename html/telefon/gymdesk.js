let daten = null;
let chefGym = 0;
let beschaeftigt = false;

const initialen = (name) => (name || "?").split(" ").map((t) => t[0]).join("").slice(0, 2).toUpperCase();

function seite(name) {
  for (const b of document.querySelectorAll(".hx-segment button")) b.classList.toggle("is-aktiv", b.dataset.seite === name);
  $("seite-training").hidden = name !== "training";
  $("seite-studio").hidden = name !== "studio";
  $("seite-kurse").hidden = name !== "kurse";
}

for (const b of document.querySelectorAll(".hx-segment button")) b.onclick = () => seite(b.dataset.seite);

function kopf(d) {
  const s = d.studio;
  $("gym").textContent = s ? s.gym : "GymDesk";
  $("rang").textContent = s ? s.rang || "Team" : "Du arbeitest in keinem Gym.";
  const dienst = $("dienst");
  dienst.hidden = !s;
  dienst.textContent = s && s.dienst ? "im Dienst" : "nicht im Dienst";
  dienst.classList.toggle("is-an", !!(s && s.dienst));
  const chef = (d.chef || []).length > 0;
  const kurse = !!s;
  $("kurs-neu").hidden = !(s && s.kurse);
  $("segment").hidden = !chef && !kurse;
  $("segment-studio").hidden = !chef;
  $("segment").style.gridTemplateColumns = chef ? "1fr 1fr 1fr" : "1fr 1fr";
  if (!chef && !$("seite-studio").hidden) seite("training");
  if (!chef && !kurse) seite("training");
}

function training(d) {
  const s = d.studio || {};
  const t = d.trainerDaten;
  const hinweis = $("kein-trainer");
  if (!s.trainer || !t) {
    hinweis.hidden = false;
    hinweis.textContent = !s.dienst
      ? "Geh in den Dienst, dann kannst du Leute coachen."
      : "Coachen dürfen Trainer und Chefs. An der Rezeption verkaufst du Mitgliedschaften und Shop-Artikel am Empfang.";
    $("training").hidden = true;
    return;
  }
  hinweis.hidden = true;
  $("training").hidden = false;
  if ($("preis").value === "") $("preis").value = t.preis;
  $("steuer").textContent = `abzüglich ${t.steuer} % Steuer`;

  const box = $("kunden");
  box.textContent = "";
  const kunden = t.kunden || [];
  if (!kunden.length) box.append(text("div", "hx-leer", "Gerade coachst du niemanden."));
  for (const k of kunden) {
    const laeuft = k.status === "laeuft";
    const uebungen = `${k.einheiten} ${k.einheiten === 1 ? "Übung" : "Übungen"}`;
    box.append(eintrag({
      symbol: text("span", "hx-avatar" + (laeuft ? " is-live" : ""), initialen(k.name)),
      titel: k.name,
      unter: laeuft ? (k.bis ? `läuft bis ${k.bis}` : "läuft, bis du abrechnest") : `beendet um ${k.bis}`,
      marke: laeuft ? "läuft" : "offen",
      markeAn: laeuft,
      infos: [["hantel", uebungen + (k.uebung ? ` · gerade ${k.uebung}` : "")]],
      knoepfe: [aktion("Abrechnen", "abrechnen", k, true), aktion("Verwerfen", "verwerfen", k, false)],
    }));
  }
}

function aktion(titel, art, kunde, voll) {
  const id = kunde.id;
  const k = text("button", "hx-knopf" + (voll ? " is-voll" : ""), titel);
  k.type = "button";
  k.onclick = async () => {
    if (beschaeftigt) return;
    const betrag = Math.max(0, parseInt($("preis").value, 10) || 0);
    beschaeftigt = true;
    const ok = art === "abrechnen"
      ? await frage("Rechnung senden", `${geld(betrag)} an ${kunde.name}`, "Senden")
      : await frage("Coaching verwerfen", `${kunde.name} zahlt nichts, der Trainer-Bonus wird trotzdem gutgeschrieben.`, "Verwerfen", true);
    if (!ok) {
      beschaeftigt = false;
      return;
    }
    k.disabled = true;
    k.textContent = art === "abrechnen" ? "Gesendet" : "Verworfen";
    const d = await nui("deskTraining", { aktion: art, id, betrag });
    beschaeftigt = false;
    if (d && d.studio !== undefined) zeigen(d);
  };
  return k;
}

$("neu").onclick = async () => {
  $("neu").textContent = "Lädt";
  const d = await nui("deskTraining", { aktion: "liste" });
  $("neu").textContent = "Aktualisieren";
  if (d && d.studio !== undefined) zeigen(d);
};

function feld(box, label, wert, schluessel) {
  const z = el("div", "hx-zeile");
  const f = el("label", "hx-feld", "$");
  const i = document.createElement("input");
  i.type = "number";
  i.min = 0;
  i.value = wert;
  i.dataset.schluessel = schluessel;
  f.append(i);
  z.append(text("span", "", label), f);
  box.append(z);
}

function studio(d) {
  const liste = d.chef || [];
  if (!liste.length) return;
  if (!liste.find((g) => g.id === chefGym)) chefGym = liste[0].id;
  const g = liste.find((x) => x.id === chefGym);
  $("tarife").textContent = "";
  feld($("tarife"), "1 Tag", g.tag, "tag");
  feld($("tarife"), "7 Tage", g.woche, "woche");
  feld($("tarife"), "30 Tage", g.monat, "monat");
  $("shop").textContent = "";
  for (const s of g.shop || []) feld($("shop"), s.name, s.preis, s.item);

  const team = $("team");
  team.textContent = "";
  if (!(g.mitarbeiter || []).length) team.append(text("div", "hx-leer", "Gerade niemand online."));
  for (const m of g.mitarbeiter || []) {
    const z = el("div", "hx-zeile");
    const t = el("span");
    t.append(document.createTextNode(m.name));
    t.append(text("small", "", m.rang || ""));
    const p = el("span", "hx-punkt-klein" + (m.dienst ? " is-an" : ""));
    z.append(text("span", "hx-avatar", initialen(m.name)), t, p);
    team.append(z);
  }
}

$("speichern").onclick = async () => {
  const zahl = (i) => Math.max(0, parseInt(i.value, 10) || 0);
  const preise = { id: chefGym, shop: {} };
  for (const i of $("tarife").querySelectorAll("input")) preise[i.dataset.schluessel] = zahl(i);
  for (const i of $("shop").querySelectorAll("input")) preise.shop[i.dataset.schluessel] = zahl(i);
  $("speichern").textContent = "Wird gespeichert";
  const d = await nui("deskPreise", preise);
  $("speichern").textContent = d ? "Gespeichert" : "Preise speichern";
  setTimeout(() => ($("speichern").textContent = "Preise speichern"), 1800);
  if (d && d.studio !== undefined) zeigen(d);
};

function tageFuellen() {
  const wahl = $("kurs-tag");
  if (wahl.options.length) return;
  for (let h = 0; h < 24; h++) {
    const o = text("option", "", String(h).padStart(2, "0"));
    o.value = String(h).padStart(2, "0");
    $("kurs-stunde").append(o);
  }
  for (const m of ["00", "15", "30", "45"]) {
    const o = text("option", "", m);
    o.value = m;
    $("kurs-minute").append(o);
  }
  $("kurs-stunde").value = "20";
  for (let i = 0; i < 7; i++) {
    const t = new Date(Date.now() + i * 86400000);
    const o = text("option", "", i === 0 ? "Heute" : i === 1 ? "Morgen" : t.toLocaleDateString("de-DE", { weekday: "long", day: "numeric", month: "numeric" }));
    o.value = i;
    wahl.append(o);
  }
}

const KURS_FRAGE = {
  absagen: ["Kurs absagen", "Alle Angemeldeten bekommen eine Nachricht.", "Absagen"],
  abbauen: ["Matten abbauen", "Die gelegten Matten werden entfernt.", "Abbauen"],
};

function kursAktion(titel, voll, aktion, id) {
  return knopf(titel, voll, async (b) => {
    const f = KURS_FRAGE[aktion];
    if (f && !(await frage(f[0], f[1], f[2], true))) return;
    b.disabled = true;
    const r = await nui("deskKurs", { aktion, id });
    if (r && r.studio !== undefined) zeigen(r);
  });
}

function formularFuellen(d) {
  const wahl = $("kurs-uebung");
  if (!wahl.options.length) {
    for (const a of d.kursArten || []) {
      const o = text("option", "", a.name);
      o.value = a.id;
      o.dataset.joggen = a.joggen ? "1" : "";
      wahl.append(o);
    }
    wahl.onchange = routeZeile;
  }
  $("kurs-max").max = d.maxTeilnehmer || 20;
  $("kurs-preis").max = d.maxPreis || 5000;
  const routen = $("kurs-route");
  const vorher = routen.value;
  routen.textContent = "";
  for (const r of d.routen || []) {
    const o = text("option", "", r.name);
    o.value = r.id;
    routen.append(o);
  }
  if (!(d.routen || []).length) routen.append(text("option", "", "Keine freie Strecke in der Nähe"));
  if (vorher) routen.value = vorher;
  routeZeile();
}

function routeZeile() {
  const o = $("kurs-uebung").selectedOptions[0];
  const joggen = !!(o && o.dataset.joggen);
  $("kurs-route-zeile").hidden = !joggen;
  $("kurs-tipp").hidden = joggen;
}

function kursListe(d) {
  tageFuellen();
  formularFuellen(d);
  const box = $("kurs-liste");
  box.textContent = "";
  const gymId = d.studio ? d.studio.gymId : 0;
  const liste = (d.kurse || []).filter((k) => k.gymId === gymId);
  if (!liste.length) box.append(text("div", "hx-leer", "Noch kein Kurs geplant."));
  for (const k of liste) {
    const knoepfe = [];
    if (k.mitarbeiter && !k.laeuft && !k.joggen && k.aufgebaut && (k.matten || 0) < k.max + 1)
      knoepfe.push(kursAktion(`Weiter legen (${Math.max(0, (k.matten || 1) - 1)}/${k.max})`, true, "aufbauen", k.id));
    if (k.mitarbeiter && !k.laeuft) knoepfe.push(kursAktion(k.aufgebaut ? "Abbauen" : (k.joggen ? "Start aufstellen" : "Matten legen"), !k.aufgebaut, k.aufgebaut ? "abbauen" : "aufbauen", k.id));
    if (k.meiner && k.aufgebaut && !k.laeuft) knoepfe.push(kursAktion(`Starten (${k.bereit} bereit)`, true, "starten", k.id));
    if (k.mitarbeiter && k.laeuft && k.aufgebaut) knoepfe.push(kursAktion("Abbauen", false, "abbauen", k.id));
    if (k.meiner && !k.laeuft) knoepfe.push(kursAktion("Absagen", false, "absagen", k.id));
    box.append(eintrag({
      symbol: text("span", "hx-avatar" + (k.laeuft ? " is-live" : ""), initialen(k.titel)),
      titel: k.titel,
      unter: `${k.uebung} · ${k.laeuft ? "läuft gerade" : k.zeit} · ${k.dauer} Min.`,
      marke: k.laeuft ? "läuft" : k.aufgebaut ? "aufgebaut" : "",
      markeAn: true,
      infos: [["ort", k.ort || "Kursort"], ["person", k.leiter], ["gruppe", `${k.teilnehmer} von ${k.max} angemeldet`], ["preis", preisText(k.preis)]],
      knoepfe,
    }));
  }
}

$("kurs-planen").onclick = async () => {
  const titel = $("kurs-titel").value.trim();
  if (titel.length < 2) {
    $("kurs-titel").focus();
    return;
  }
  $("kurs-planen").textContent = "Wird geplant";
  const r = await nui("deskKurs", {
    aktion: "anlegen", titel, preis: Math.max(0, parseInt($("kurs-preis").value, 10) || 0), tag: parseInt($("kurs-tag").value, 10) || 0,
    uhrzeit: `${$("kurs-stunde").value}:${$("kurs-minute").value}`, dauer: parseInt($("kurs-dauer").value, 10) || 60,
    uebung: $("kurs-uebung").value, max: parseInt($("kurs-max").value, 10) || 10, route: parseInt($("kurs-route").value, 10) || 0,
  });
  $("kurs-planen").textContent = "Kurs planen";
  if (r && r.studio !== undefined) {
    $("kurs-titel").value = "";
    zeigen(r);
  }
};

function zeigen(d) {
  daten = d;
  datumKopf();
  kopf(d);
  training(d);
  kursListe(d);
  studio(d);
}

async function holen() {
  const d = await nui("deskDaten");
  if (d && d.studio !== undefined) zeigen(d);
}

window.addEventListener("message", (e) => {
  if (e.data === "componentsLoaded") holen();
});

holen();

if (!imSpiel) {
  zeigen({
    studio: { gymId: 1, gym: "Muscle Beach Gym", rang: "Chefin", dienst: true, chef: true, trainer: true, kurse: true },
    maxTeilnehmer: 20, maxPreis: 5000,
    kursArten: [{ id: "flexibilitaet:kurs-yoga", name: "Yoga-Kurs" }, { id: "flexibilitaet:kurs-pilates", name: "Pilates-Kurs" }, { id: "kraft:kurs-hanteln", name: "Hantel-Kurs" }, { id: "kondition:kurs-situps", name: "Sit-up-Kurs" }, { id: "kondition:kurs-liegestuetze", name: "Liegestütz-Kurs" }, { id: "ausdauer:kurs-joggen", name: "Lauf-Kurs", joggen: true }],
    routen: [{ id: 14, name: "1,4 km · Del Perro Fwy, Vespucci Beach" }],
    kurse: [
      { id: 1, gymId: 1, titel: "Yoga am Strand", uebung: "Yoga-Kurs", zeit: "Heute 20:00", dauer: 60, ort: "Bay City Ave, Vespucci Beach", preis: 50, leiter: "Lena Vogt", teilnehmer: 7, max: 10, bereit: 3, aufgebaut: true, matten: 7, laeuft: false, meiner: true, mitarbeiter: true },
      { id: 2, gymId: 1, titel: "Morgenlauf", uebung: "Lauf-Kurs", joggen: true, zeit: "Morgen 08:30", dauer: 45, ort: "Del Perro Fwy, Vespucci Beach", preis: 0, leiter: "Jonas Becker", teilnehmer: 3, max: 12, bereit: 0, aufgebaut: false, laeuft: false, meiner: false, mitarbeiter: true },
    ],
    minuten: 30,
    trainerDaten: { preis: 150, steuer: 5, kunden: [
      { id: 5, name: "Max Muster", status: "laeuft", einheiten: 2, bis: "22:40", uebung: "Bankdrücken" },
      { id: 6, name: "Anni Kowalski", status: "offen", einheiten: 3, bis: "21:55", uebung: "" },
    ] },
    chef: [{ id: 1, name: "Muscle Beach Gym", tag: 100, woche: 500, monat: 1500,
      shop: [{ item: "proteinshake", name: "Protein-Shake", preis: 80 }, { item: "vitamine", name: "Vitamin-Kapseln", preis: 120 }, { item: "muskelbalsam", name: "Muskelbalsam", preis: 60 }],
      mitarbeiter: [{ name: "Lena Vogt", rang: "Trainerin", dienst: true }, { name: "Jonas Becker", rang: "Empfang", dienst: false }] }],
  });
  if (location.hash === "#studio") seite("studio");
  if (location.hash === "#kurse") seite("kurse");
}
