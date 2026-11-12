// toter-fitness: Werte-Übersicht und die vier Minispiele.
const $ = (id) => document.getElementById(id);

const STAT_NAMEN = { kraft: "Kraft", ausdauer: "Ausdauer", flexibilitaet: "Flexibilität", kondition: "Kondition" };

// ------------------------------------------------------------------ Fitness-Menü
let menue = null;      // letzte Daten vom Client
let bestaetigen = 0;   // Station, deren Löschen gerade bestätigt werden muss

function el(tag, klasse, text) {
  const e = document.createElement(tag);
  if (klasse) e.className = klasse;
  if (text !== undefined) e.textContent = text;
  return e;
}

function knopf(text, klick, klasse) {
  const b = el("button", "tu-btn" + (klasse ? " " + klasse : ""), text);
  b.type = "button";
  b.addEventListener("click", klick);
  return b;
}

function befehl(text) {
  Nui.post("menueBefehl", { befehl: text });
}

function gymAktion(aktion, daten) {
  Nui.post("gymAktion", { aktion, daten });
}

let gymLoeschen = 0;
let gymOffen = 0;

function gymsZeichnen(d) {
  const gyms = d.gyms || [];
  const liste = $("gym-liste");
  liste.textContent = "";
  if (!gyms.length) liste.append(el("li", "tu-hint", "Noch kein Gym angelegt."));
  for (const g of gyms) {
    const li = el("li", "tu-list-item fx-gym-zeile");
    const kopf = el("div", "fx-gym-kopf");
    const links = el("div");
    links.append(el("b", "", g.name), " ", el("small", "", (g.stationen === 1 ? "1 Station" : `${g.stationen} Stationen`) + (g.empfang ? " · Empfang" : "")));
    const offen = gymOffen === g.id;
    kopf.append(links, knopf(offen ? "Zu" : "Einstellungen", () => { gymOffen = offen ? 0 : g.id; gymsZeichnen(menue); }),
      knopf(gymLoeschen === g.id ? "Wirklich?" : "Löschen", () => {
        if (gymLoeschen === g.id) { gymLoeschen = 0; gymAktion("loeschen", { id: g.id }); return; }
        gymLoeschen = g.id;
        gymsZeichnen(menue);
        setTimeout(() => { if (gymLoeschen === g.id) { gymLoeschen = 0; gymsZeichnen(menue); } }, 4000);
      }, "is-danger"));
    li.append(kopf);
    if (offen) {
      const mehr = el("div", "fx-gym-mehr");
      const job = el("input", "tu-input");
      job.placeholder = "Job der Mitarbeiter, z. B. gym (leer = Selbstbedienung)";
      job.value = g.job || "";
      const preise = el("div", "fx-gym-preise");
      const felder = {};
      for (const [k, t] of [["tag", "1 Tag"], ["woche", "7 Tage"], ["monat", "30 Tage"]]) {
        const f = el("div");
        const i = el("input", "tu-input");
        i.type = "number";
        i.min = 0;
        i.value = g[k] || 0;
        felder[k] = i;
        f.append(el("label", "tu-label", t), i);
        preise.append(f);
      }
      const shopBox = el("div", "fx-gym-preise");
      const shopFelder = {};
      for (const sup of menue.supplements || []) {
        const f = el("div");
        const i = el("input", "tu-input");
        i.type = "number";
        i.min = 0;
        i.value = (g.shop && g.shop[sup.item] != null) ? g.shop[sup.item] : 0;
        i.title = "0 = in diesem Gym nicht im Angebot";
        shopFelder[sup.item] = i;
        f.append(el("label", "tu-label", sup.name), i);
        shopBox.append(f);
      }
      const kn = el("div", "fx-gym-knoepfe");
      kn.append(
        knopf("Speichern", () => gymAktion("einstellungen", {
          id: g.id, job: job.value.trim(),
          tag: parseInt(felder.tag.value, 10) || 0, woche: parseInt(felder.woche.value, 10) || 0, monat: parseInt(felder.monat.value, 10) || 0,
          shop: Object.fromEntries(Object.entries(shopFelder).map(([k, i]) => [k, Math.max(0, parseInt(i.value, 10) || 0)])),
        }), "is-primary"),
        knopf("Empfang hier setzen", () => gymAktion("empfangSetzen", { id: g.id }))
      );
      if (g.empfang) kn.append(knopf("Empfang entfernen", () => gymAktion("empfangLoeschen", { id: g.id }), "is-danger"));
      mehr.append(el("label", "tu-label", "Mitarbeiter-Job"), job, el("label", "tu-label", "Mitgliedschaft ($)"), preise,
        el("label", "tu-label", "Shop ($, 0 = nicht im Angebot)"), shopBox, kn);
      li.append(mehr);
    }
    liste.append(li);
  }
  const wahl = $("karte-gym-wahl");
  const vorher = wahl.value;
  wahl.textContent = "";
  for (const g of gyms) {
    const o = el("option", "", g.name);
    o.value = g.id;
    wahl.append(o);
  }
  if (vorher) wahl.value = vorher;
  $("karte-los").disabled = gyms.length === 0;
  if (!$("karte-ziel").value) { $("karte-ziel").value = d.meineId || ""; $("karte-ziel").dataset.auto = "1"; }
}

function stationInfo(s, preis) {
  const info = el("div", "fx-station-info");
  info.append(STAT_NAMEN[s.stat] || s.stat, " · ");
  info.append(s.studio ? el("span", "is-studio", s.gym ? `Mitglieder ${s.gym}` : `Studio $${preis}`) : "kostenlos");
  info.append(` · ${s.abstand} m`);
  return info;
}

function adminZeichnen(d) {
  const auswahl = $("neu-uebung");
  const vorher = auswahl.value;
  auswahl.textContent = "";
  for (const u of d.uebungen || []) {
    const o = el("option", "", `${STAT_NAMEN[u.stat]}: ${u.name} (${u.art === "studio" ? "Studio" : "frei"})`);
    o.value = `${u.stat} ${u.art}`;
    o.dataset.prop = u.prop;
    auswahl.append(o);
  }
  if (vorher) auswahl.value = vorher;
  if (!$("sp-id").value) $("sp-id").value = d.meineId || "";

  gymsZeichnen(d);
  const liste = $("admin-liste");
  liste.textContent = "";
  for (const s of d.stationen || []) {
    const li = el("li", "fx-station is-admin");
    const kopf = el("div");
    const name = el("div", "fx-station-name", `#${s.id} ${s.name}`);
    const info = stationInfo(s, d.preis);
    info.append(` · ${s.prop || "ohne Prop"}` + (s.hoehe ? ` · Höhe ${s.hoehe}` : ""));
    if (s.joggen) info.append(` · Strecke: ${s.route} Punkte`);
    kopf.append(name, info);

    const k = el("div", "fx-station-knoepfe");
    const g1 = el("span", "fx-gruppe");
    g1.append(
      knopf("Versetzen", () => Nui.post("platzieren", { station: s.id, stat: s.stat, art: s.studio ? "studio" : "frei" })),
      knopf("Hierher", () => befehl(`fitnessmove ${s.id}`)),
      knopf("Wegpunkt", () => Nui.post("wegpunkt", { id: s.id })),
      knopf("Teleport", () => Nui.post("teleport", { id: s.id }))
    );
    const g2 = el("span", "fx-gruppe");
    g2.append(
      knopf("Höhe −", () => befehl(`fitnesshoehe ${s.id} -0.05`)),
      knopf("Höhe +", () => befehl(`fitnesshoehe ${s.id} 0.05`)),
      knopf("⟲ 15°", () => befehl(`fitnessdrehen ${s.id} -15`)),
      knopf("⟳ 15°", () => befehl(`fitnessdrehen ${s.id} 15`)),
      knopf("90°", () => befehl(`fitnessdrehen ${s.id} 90`))
    );
    k.append(g1, g2);
    if (s.studio) {
      const g5 = el("span", "fx-gruppe");
      const wahl = el("select", "tu-select fx-gym-wahl");
      const ohne = el("option", "", `ohne Gym ($${d.preis} pro Training)`);
      ohne.value = 0;
      wahl.append(ohne);
      for (const g of d.gyms || []) {
        const o = el("option", "", g.name);
        o.value = g.id;
        wahl.append(o);
      }
      wahl.value = s.gymId || 0;
      wahl.addEventListener("change", () => gymAktion("station", { station: s.id, gym: parseInt(wahl.value, 10) }));
      g5.append(wahl);
      k.append(g5);
    }
    if (s.joggen) {
      const g3 = el("span", "fx-gruppe");
      g3.append(
        knopf("+ Punkt", () => befehl(`fitnessroute ${s.id} add`)),
        knopf("Punkt weg", () => befehl(`fitnessroute ${s.id} undo`)),
        knopf("Zeigen", () => befehl(`fitnessroute ${s.id} show`)),
        knopf("Strecke leeren", () => befehl(`fitnessroute ${s.id} clear`), "is-danger")
      );
      k.append(g3);
    }
    const g4 = el("span", "fx-gruppe");
    const loeschen = knopf(bestaetigen === s.id ? "Wirklich löschen?" : "Löschen", () => {
      if (bestaetigen === s.id) {
        bestaetigen = 0;
        befehl(`fitnessdelete ${s.id}`);
      } else {
        bestaetigen = s.id;
        adminZeichnen(menue);
        setTimeout(() => { if (bestaetigen === s.id) { bestaetigen = 0; adminZeichnen(menue); } }, 4000);
      }
    }, "is-danger");
    g4.append(loeschen);
    k.append(g4);
    li.append(kopf, k);
    liste.append(li);
  }
}

// ------------------------------------------------------------------ Diagnose und Rückmeldungen
for (const b of document.querySelectorAll("[data-diagnose]"))
  b.addEventListener("click", () => Nui.post("diagnose", { art: b.dataset.diagnose }));

Nui.on("diagnose", ({ zeilen }) => {
  $("diagnose-ausgabe").textContent = (zeilen || []).join("\n");
  $("diagnose-ausgabe").hidden = false;
});

Nui.on("ausgabe", ({ text }) => Nui.toast("Fitness", text, "", 6000));

$("neu-setzen").addEventListener("click", () => {
  const o = $("neu-uebung").selectedOptions[0];
  if (!o) return;
  const prop = $("neu-prop").value.trim();
  befehl(`fitnessplace ${o.value}${prop ? " " + prop : ""}`);
  $("neu-prop").value = "";
});

$("neu-platzieren").addEventListener("click", () => {
  const o = $("neu-uebung").selectedOptions[0];
  if (!o) return;
  const [stat, art] = o.value.split(" ");
  Nui.post("platzieren", { stat, art, prop: $("neu-prop").value.trim(), station: 0 });
});

Nui.on("menueZu", () => Nui.hide($("stats")));

$("neu-vorschau").addEventListener("click", () => {
  const o = $("neu-uebung").selectedOptions[0];
  const modell = $("neu-prop").value.trim() || (o && o.dataset.prop) || "";
  if (modell && modell !== "-") Nui.post("propVorschau", { modell });
});

$("neu-uebung").addEventListener("change", () => {
  const o = $("neu-uebung").selectedOptions[0];
  $("neu-prop").placeholder = o && o.dataset.prop ? o.dataset.prop : "ohne Prop";
});

$("gym-neu").addEventListener("click", () => {
  const name = $("gym-name").value.trim();
  if (name.length < 2) return;
  gymAktion("neu", { name });
  $("gym-name").value = "";
});

$("karte-los").addEventListener("click", () => {
  const gym = parseInt($("karte-gym-wahl").value, 10);
  const ziel = parseInt($("karte-ziel").value, 10);
  const dauer = Math.max(1, parseFloat($("karte-dauer").value) || 0);
  if (!gym || !ziel) return;
  gymAktion("karte", { gym, ziel, sekunden: Math.round(dauer * parseInt($("karte-einheit").value, 10)) });
});

// ------------------------------------------------------------------ Gym-Empfang
let empfang = null;
let tarifWahl = "monat";

Nui.on("empfang", (d) => {
  empfang = d;
  $("empfang-titel").textContent = d.gym;
  $("empfang-unter").textContent = d.modus === "verkauf" ? "Verkauf (Mitarbeiter)" : d.modus === "kauf" ? "Selbstbedienung" : "Preise";
  $("empfang-karte").hidden = !d.meineKarte;
  $("empfang-karte").textContent = d.meineKarte ? `Deine Mitgliedschaft läuft bis ${d.meineKarte}. Ein Kauf verlängert sie.` : "";
  const verkauf = d.modus === "verkauf";
  $("empfang-kunden-feld").hidden = !verkauf;
  const kunden = d.kunden || [];
  const wahl = $("empfang-kunde");
  wahl.textContent = "";
  for (const k of kunden) {
    const o = el("option", "", `${k.name} (ID ${k.id})`);
    o.value = k.id;
    wahl.append(o);
  }
  wahl.hidden = kunden.length === 0;
  $("empfang-keiner").hidden = !verkauf || kunden.length > 0;
  $("empfang-info").hidden = d.modus !== "info";
  $("empfang-shop-titel").textContent = d.modus === "verkauf" ? "Supplements anbieten" : d.modus === "info" ? "Preise Supplements" : "Supplements";
  $("empfang-probetag").hidden = !d.probetag;
  $("empfang-info").textContent = d.probetag
    ? "Mitgliedschaften und Supplements verkaufen die Mitarbeiter. Den Probetag kannst du einmal selbst buchen."
    : "Hier siehst du nur die Preise. Kaufen kannst du bei den Mitarbeitern am Empfang.";
  $("empfang-probetag-knopf").textContent = d.probetag ? `Probetag $${d.probetag}` : "";
  $("empfang-tarife-titel").textContent = d.modus === "info" ? "Preise Mitgliedschaft" : "Mitgliedschaft";
  const los = $("empfang-los");
  los.hidden = d.modus === "info";
  los.textContent = verkauf ? "Angebot schicken" : "Kaufen";
  los.disabled = verkauf && kunden.length === 0;
  tarifeZeichnen();
  const shop = $("empfang-shop");
  shop.textContent = "";
  for (const it of d.shop || []) {
    const li = el("li");
    const links = el("div");
    links.append(el("b", "", it.name), el("small", "", it.text));
    const beschriftung = d.modus === "verkauf" ? `Anbieten · $${it.preis}` : `$${it.preis}`;
    li.append(links, d.modus === "info" ? el("span", "fx-preis", `$${it.preis}`) : knopf(beschriftung, () => shopKlick(it.item)));
    shop.append(li);
  }
  Nui.hide($("stats"));
  Nui.show($("empfang"));
});

function tarifeZeichnen() {
  const box = $("empfang-tarife");
  box.textContent = "";
  for (const t of empfang.tarife || []) {
    const info = empfang.modus === "info";
    const b = el("div", "fx-tarif" + (info ? " is-info" : t.id === tarifWahl ? " is-aktiv" : ""));
    b.append(el("b", "", t.name), el("span", "", `$${t.preis}`));
    if (empfang.modus !== "info") b.addEventListener("click", () => { tarifWahl = t.id; tarifeZeichnen(); });
    box.append(b);
  }
}

$("empfang-probetag-knopf").addEventListener("click", () => {
  if (!empfang || !empfang.probetag) return;
  Nui.post("empfangKaufen", { gymId: empfang.gymId, art: "probetag", was: "" });
  Nui.hide($("empfang"));
});

$("empfang-los").addEventListener("click", () => {
  if (!empfang) return;
  if (empfang.modus === "verkauf") {
    const kunde = parseInt($("empfang-kunde").value, 10);
    if (kunde) Nui.post("empfangAnbieten", { gymId: empfang.gymId, kunde, art: "mitgliedschaft", was: tarifWahl });
  } else if (empfang.modus === "kauf") {
    Nui.post("empfangKaufen", { gymId: empfang.gymId, art: "mitgliedschaft", was: tarifWahl });
    Nui.hide($("empfang"));
  }
});

function shopKlick(item) {
  if (!empfang) return;
  if (empfang.modus === "verkauf") {
    const kunde = parseInt($("empfang-kunde").value, 10);
    if (kunde) Nui.post("empfangAnbieten", { gymId: empfang.gymId, kunde, art: "item", was: item });
  } else if (empfang.modus === "kauf") {
    Nui.post("empfangKaufen", { gymId: empfang.gymId, art: "item", was: item });
  }
}

let angebotTimer = 0;
function angebotBeantworten(ja) {
  clearInterval(angebotTimer);
  Nui.post("angebotAntwort", { ja });
  Nui.hide($("angebot"));
}

Nui.on("angebot", (d) => {
  $("angebot-titel").textContent = d.titel;
  if (d.art === "coaching") $("angebot-text").textContent = `${d.verkaeufer} möchte dich coachen: ${d.text}.`;
  else if (d.art === "rechnung") $("angebot-text").textContent = `${d.verkaeufer} schickt dir die Rechnung für ${d.text}. Bezahlt wird von deinem Konto.`;
  else $("angebot-text").textContent = `${d.verkaeufer} bietet dir an: ${d.text}. Bezahlt wird gleich von deinem Konto.`;
  $("angebot-ja").textContent = d.art === "coaching" ? "Annehmen" : d.art === "rechnung" ? "Bezahlen" : "Annehmen und bezahlen";
  $("angebot-nein").textContent = "Ablehnen";
  $("angebot-preis").textContent = d.art === "coaching" ? "Preis am Ende" : `$${d.preis}`;
  let rest = d.sekunden || 60;
  $("angebot-zeit").textContent = `noch ${rest} s`;
  clearInterval(angebotTimer);
  angebotTimer = setInterval(() => {
    rest--;
    $("angebot-zeit").textContent = `noch ${rest} s`;
    if (rest <= 0) angebotBeantworten(false);
  }, 1000);
  Nui.show($("angebot"));
});

$("angebot-ja").addEventListener("click", () => angebotBeantworten(true));
$("angebot-nein").addEventListener("click", () => angebotBeantworten(false));

// ------------------------------------------------------------------ Mitgliedskarte zeigen
let karteTimer = 0;
Nui.on("karte", (d) => {
  $("karte-gym").textContent = d.gym || "Fitness Club";
  $("karte-name").textContent = d.name || "-";
  $("karte-geburt").textContent = d.geburtstag || "-";
  $("karte-nummer").textContent = d.nummer || "-";
  $("karte-ablauf").textContent = d.ablauf || "-";
  const rest = $("karte-rest");
  const tage = d.restTage || 0;
  rest.textContent = tage >= 1 ? `noch ${tage} ${tage === 1 ? "Tag" : "Tage"}` : "läuft heute ab";
  rest.classList.toggle("is-knapp", tage < 3);
  $("karte-unter").textContent = d.eigene ? "" : `${d.zeiger || "Jemand"} zeigt dir eine Mitgliedskarte`;
  const platz = $("karte-platz");
  platz.classList.remove("is-weg");
  platz.hidden = false;
  clearTimeout(karteTimer);
  karteTimer = setTimeout(() => {
    platz.classList.add("is-weg");
    setTimeout(() => { platz.hidden = true; platz.classList.remove("is-weg"); }, 360);
  }, 8000);
});

// ------------------------------------------------------------------ Admin: Unter-Reiter
let adminUnter = "stationen";

function unterZeigen(name) {
  adminUnter = name;
  for (const b of document.querySelectorAll("#admin-unter button")) b.classList.toggle("is-active", b.dataset.unter === name);
  for (const s of document.querySelectorAll(".fx-unterseite")) s.hidden = s.dataset.unter !== name;
  if (name === "kurse") gymAktion("kurseAlle", {});
  if (name === "besten" && $("admin-besten-uebung").value) gymAktion("besten", { uebung: $("admin-besten-uebung").value });
}

for (const b of document.querySelectorAll("#admin-unter button")) b.addEventListener("click", () => unterZeigen(b.dataset.unter));

function adminBestenAuswahl(liste) {
  const wahl = $("admin-besten-uebung");
  if (wahl.options.length || !liste.length) return;
  for (const u of liste) {
    const o = el("option", "", `${STAT_NAMEN[u.stat] || u.stat}: ${u.name}`);
    o.value = u.id;
    wahl.append(o);
  }
}

$("admin-besten-uebung").addEventListener("change", () => gymAktion("besten", { uebung: $("admin-besten-uebung").value }));

let bestenResetFrage = false;
$("admin-besten-reset").addEventListener("click", () => {
  const knopfReset = $("admin-besten-reset");
  if (!bestenResetFrage) {
    bestenResetFrage = true;
    knopfReset.textContent = "Wirklich alles löschen?";
    setTimeout(() => { bestenResetFrage = false; knopfReset.textContent = "Liste zurücksetzen"; }, 4000);
    return;
  }
  bestenResetFrage = false;
  knopfReset.textContent = "Liste zurücksetzen";
  gymAktion("bestenReset", { uebung: $("admin-besten-uebung").value });
});

Nui.on("adminDaten", ({ art, daten }) => {
  if (art === "kurse") {
    const ul = $("admin-kurse");
    ul.textContent = "";
    for (const k of daten || []) {
      const li = el("li", "fx-admin-zeile");
      const text = el("div");
      text.append(el("b", "", k.titel), el("small", "", `${k.gym} · ${k.uebung} · ${k.zeit} · ${k.leiter} · ${k.teilnehmer}/${k.max}${k.laeuft ? " · läuft" : ""}`));
      li.append(text, knopf("Absagen", () => gymAktion("kursAbsagen", { id: k.id }), "is-danger"));
      ul.append(li);
    }
    if (!(daten || []).length) ul.append(el("li", "fx-admin-leer", "Keine Kurse geplant."));
  } else if (art === "besten") {
    const ul = $("admin-besten");
    ul.textContent = "";
    (daten.liste || []).forEach((e, i) => {
      const li = el("li", "fx-admin-zeile");
      const text = el("div");
      text.append(el("b", "", `${i + 1}. ${e.name}`), el("small", "", `${e.wert} ${daten.einheit} · ${e.datum}`));
      li.append(text, knopf("Löschen", () => gymAktion("bestenLoeschen", { uebung: daten.uebung, cid: e.cid }), "is-danger"));
      ul.append(li);
    });
    if (!(daten.liste || []).length) ul.append(el("li", "fx-admin-leer", "Noch keine Einträge."));
  }
});

// ------------------------------------------------------------------ Admin: Spieler-Werte
let spieler = null;

function spielerLaden() {
  const ziel = parseInt($("sp-id").value, 10);
  if (ziel) gymAktion("spieler", { ziel });
}

$("sp-laden").addEventListener("click", spielerLaden);
$("sp-id").addEventListener("keydown", (e) => { if (e.key === "Enter") spielerLaden(); });

Nui.on("spielerWerte", (d) => {
  $("sp-fehler").hidden = !d.fehler;
  $("sp-fehler").textContent = d.fehler || "";
  $("sp-inhalt").hidden = !!d.fehler;
  $("sp-bereiche").hidden = !!d.fehler;
  if (d.fehler) return;
  spieler = d;
  $("sp-kater").textContent = d.kater === "morgen" ? "Muskelkater ab morgen früh" : d.kater ? `Muskelkater bis ${d.kater}` : "Kein Muskelkater";
  $("sp-kater-knopf").textContent = d.kater ? "Muskelkater weg" : "Muskelkater geben";
  $("sp-shake").textContent = d.shake ? `Shake wirkt bis ${d.shake}` : "Kein Shake";
  $("sp-shake-knopf").hidden = !d.shake;
  $("sp-vitamine").textContent = d.vitamine ? `Vitamine wirken bis ${d.vitamine}` : "Keine Vitamine";
  $("sp-vitamine-knopf").hidden = !d.vitamine;
  const pt = $("sp-probetage");
  pt.textContent = "";
  for (const g of d.probetage || []) {
    const li = el("li");
    li.append(el("span", "", `Genutzt bei ${g.name}`), knopf("Wieder frei", () => gymAktion("probetag", { ziel: d.id, gym: g.id })));
    pt.append(li);
  }
  if (!(d.probetage || []).length) pt.append(el("li", "", "Noch keinen Probetag genutzt."));
  if (!$("karte-ziel").value || $("karte-ziel").dataset.auto === "1") {
    $("karte-ziel").value = d.id;
    $("karte-ziel").dataset.auto = "1";
  }
  $("sp-name").textContent = `#${d.id} ${d.name}` + (d.mitglied ? " · Gym-Mitglied (Abbau 2/Tag)" : " · ohne Mitgliedschaft (Abbau 1/Tag)");
  const liste = $("sp-liste");
  liste.textContent = "";
  for (const w of d.werte || []) {
    const li = el("li", "fx-sp-zeile");
    const name = el("div");
    name.append(el("b", "", w.name), el("small", "", `zuletzt ${w.zuletzt}`));
    const eingabe = el("input", "tu-input");
    eingabe.type = "number";
    eingabe.min = 0;
    eingabe.max = 100;
    eingabe.value = w.wert;
    eingabe.dataset.stat = w.stat;
    const frei = w.heute
      ? knopf("Freigeben", () => gymAktion("freigeben", { ziel: d.id, stat: w.stat }))
      : el("span", "fx-sp-ok", "frei");
    if (w.heute) frei.title = "Heute schon trainiert. Klicken, damit er es heute noch einmal darf.";
    li.append(name, eingabe, frei);
    liste.append(li);
  }
});

$("sp-shake-knopf").addEventListener("click", () => {
  if (spieler) gymAktion("wirkung", { ziel: spieler.id, art: "shake" });
});
$("sp-vitamine-knopf").addEventListener("click", () => {
  if (spieler) gymAktion("wirkung", { ziel: spieler.id, art: "vitamine" });
});
$("karte-ziel").addEventListener("input", () => { $("karte-ziel").dataset.auto = ""; });

$("sp-kater-knopf").addEventListener("click", () => {
  if (spieler) gymAktion("kater", { ziel: spieler.id, an: !spieler.kater });
});

$("sp-speichern").addEventListener("click", () => {
  if (!spieler) return;
  const werte = {};
  for (const i of document.querySelectorAll("#sp-liste input")) {
    werte[i.dataset.stat] = Math.max(0, Math.min(100, parseInt(i.value, 10) || 0));
  }
  gymAktion("werte", { ziel: spieler.id, werte });
});

$("sp-alle").addEventListener("click", () => {
  if (spieler) gymAktion("freigeben", { ziel: spieler.id, stat: "alle" });
});

Nui.on("menue", (d) => {
  menue = d;
  adminBestenAuswahl(d.uebungenAlle || []);
  if (d.admin) adminZeichnen(d);
  if (d.oeffnen && d.admin) {
    unterZeigen(adminUnter);
    const o = $("neu-uebung").selectedOptions[0];
    $("neu-prop").placeholder = o && o.dataset.prop ? o.dataset.prop : "ohne Prop";
    Nui.hide($("spiel"));
    Nui.show($("stats"));
  }
});

// ------------------------------------------------------------------ Spielrahmen
let spiel = null;
let restzeit = 0;
let letzterFrame = 0;

const SPIELE = {};

Nui.on("spiel", (d) => {
  const def = SPIELE[d.spiel];
  if (!def) return;
  Nui.hide($("stats"));
  for (const el of document.querySelectorAll(".fx-feld")) el.hidden = true;
  $("spiel-ergebnis").hidden = true;
  $("spiel-titel").textContent = d.titel || "Training";
  $("spiel-stat").textContent = (d.stat || "") + (d.studio ? " · Studio" : "");
  $("feld-" + d.spiel).hidden = false;
  $("spiel-hilfe").innerHTML = "";
  $("spiel-hilfe").hidden = true;
  Nui.show($("spiel"));

  // Dauer kommt vom Client (Config.cs: Studio zusammen 10 Minuten, frei 15 Minuten), sonst der Standard des Spiels.
  const dauer = d.dauer > 0 ? d.dauer : def.dauer;
  spiel = def.neu(!!d.studio, !!d.runde, dauer);
  restzeit = spiel.ohneZeit ? 600 : dauer;
  letzterFrame = performance.now();
  requestAnimationFrame(schleife);
});

// Joggen: der Client meldet, wie weit die Runde ist, und wann sie fertig ist.
Nui.on("fortschritt", (d) => {
  if (spiel && spiel.fortschritt) spiel.fortschritt(d.wert || 0);
});

Nui.on("rundeFertig", () => {
  if (spiel && spiel.rundeFertig) spiel.rundeFertig();
});

Nui.on("abbrechen", () => {
  spiel = null;
  Nui.hide($("spiel"));
});

function schleife(jetzt) {
  if (!spiel) return;
  const dt = Math.min(0.05, (jetzt - letzterFrame) / 1000);
  letzterFrame = jetzt;
  restzeit -= dt;
  spiel.update(dt);

  const zeit = $("spiel-zeit");
  if (spiel.anzeige) {
    zeit.textContent = spiel.anzeige();
    zeit.classList.remove("is-knapp");
  } else {
    const s = Math.max(0, Math.ceil(restzeit));
    zeit.textContent = s >= 60 ? `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}` : s;
    zeit.classList.toggle("is-knapp", restzeit < 5);
  }

  if (restzeit <= 0 || spiel.fertig) {
    beenden(spiel.punkte(), !!spiel.verloren, spiel.rekord ? spiel.rekord() : 0);
    return;
  }
  requestAnimationFrame(schleife);
}

function beenden(punkte, verloren, rekord) {
  spiel = null;
  punkte = Math.max(0, Math.min(100, Math.round(punkte)));
  Nui.post("ergebnis", { punkte, verloren: !!verloren, rekord: Math.max(0, Math.round(rekord || 0)) });
  const lob = punkte >= 85 ? "Stark!" : punkte >= 60 ? "Gut gemacht" : punkte >= 30 ? "Geht noch besser" : "Das war nichts";
  const box = $("spiel-ergebnis");
  box.innerHTML = "";
  const t = document.createElement("div");
  t.textContent = `${lob} · ${punkte} %`;
  const s = document.createElement("small");
  s.textContent = "Training beendet";
  t.append(s);
  box.append(t);
  box.hidden = false;
  setTimeout(() => {
    if (!spiel) Nui.hide($("spiel"));
  }, 1600);
}

window.addEventListener("keydown", (e) => {
  if (!spiel) return;
  if (e.key === "Escape") {
    spiel = null;
    return;
  }
  if ([" ", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(e.key)) e.preventDefault();
  if (spiel.taste) spiel.taste(e, true);
});

window.addEventListener("keyup", (e) => {
  if (spiel && spiel.taste) spiel.taste(e, false);
});

// --------------------------------------------- Kraft: Leertaste über die Schwelle
SPIELE.kraft = {
  dauer: 25,
  hilfe: "<kbd>Leertaste</kbd> schnell drücken. Über <b>80 %</b> zählen die Wiederholungen, dort wird es immer schwerer. Zwischen den Sätzen kurz Pause.",
  neu(studio, runde, dauer) {
    // Lange Trainings in Sätzen: 25 s Arbeit, 10 s Pause. Ziel: 12 Wiederholungen pro vollem Satz.
    const SATZ = 25, PAUSE = 10;
    const saetze = Math.max(1, Math.round((dauer + PAUSE) / (SATZ + PAUSE)));
    const ziel = 12 * saetze;
    let satz = 1, pause = 0, satzZeit = 0;
    let wert = 0;
    let last = 0;          // steigt, solange man oben hält
    let obenZeit = 0;
    let wdh = 0;
    let oben = false;
    let erreicht = false;
    const fill = $("kraft-fill");
    const zahl = $("kraft-wdh");
    const lastText = $("kraft-last");
    zahl.textContent = "0";

    function obenMelden(jetzt) {
      if (jetzt !== oben) {
        oben = jetzt;
        Nui.post("phase", { oben });
      }
    }

    return {
      fertig: false,
      update(dt) {
        if (pause > 0) {
          pause -= dt;
          wert = Math.max(0, wert - 60 * dt);
          fill.style.height = wert + "%";
          fill.classList.remove("is-oben");
          lastText.textContent = `Pause, noch ${Math.ceil(pause)} s`;
          lastText.classList.remove("is-schwer");
          if (pause <= 0) {
            satz++;
            satzZeit = 0;
            last = 0;
            erreicht = false;
          }
          return;
        }
        satzZeit += dt;
        if (satz < saetze && satzZeit >= SATZ) {
          pause = PAUSE;
          obenMelden(false);
          return;
        }
        const abbau = ((studio ? 18 : 23) + last) * (erreicht ? 1 : 0.5);
        wert = Math.max(0, wert - abbau * dt);
        const jetztOben = wert >= 80;
        if (jetztOben) erreicht = true;
        if (jetztOben) {
          last += (studio ? 3 : 4) * dt;
          obenZeit += dt;
          if (obenZeit >= 1.1) {
            obenZeit -= 1.1;
            wdh++;
            zahl.textContent = wdh;
            zahl.classList.remove("is-puls");
            void zahl.offsetWidth;
            zahl.classList.add("is-puls");
          }
        } else {
          last = Math.max(0, last - 6 * dt);
          obenZeit = 0;
        }
        obenMelden(jetztOben);
        fill.style.height = wert + "%";
        fill.classList.toggle("is-oben", oben);
        const satzText = saetze > 1 ? `Satz ${satz} / ${saetze}` : "";
        lastText.textContent = last > 16 ? "Die Arme brennen!" : last > 7 ? "Wird schwerer" : satzText;
        lastText.classList.toggle("is-schwer", last > 16);
      },
      taste(e, runter) {
        if (pause > 0) return;
        if (runter && e.key === " " && !e.repeat) wert = Math.min(100, wert + (studio ? 8 : 7.5));
      },
      punkte() {
        return (wdh / ziel) * 100;
      },
      rekord() {
        return wdh;
      },
    };
  },
};

// ------------------------------------------------ Ausdauer: Dino-Lauf
SPIELE.dino = {
  dauer: 40,
  hilfe: "<kbd>Leertaste</kbd> oder <kbd>↑</kbd> springen, <kbd>↓</kbd> ducken. Drei Leben, es wird immer schneller. Alle Leben weg = Training vorbei.",
  neu(studio, runde, dauer) {
    dauer = dauer || SPIELE.dino.dauer;
    const cv = $("dino");
    const ctx = cv.getContext("2d");
    const css = getComputedStyle(document.documentElement);
    const farbe = {
      text: css.getPropertyValue("--tu-text").trim() || "#e8eff9",
      akzent: css.getPropertyValue("--tu-accent").trim() || "#19d6f2",
      linie: "rgba(120,180,240,0.35)",
      gefahr: css.getPropertyValue("--tu-danger").trim() || "#ff3b4e",
      dim: css.getPropertyValue("--tu-text-faint").trim() || "#5f7391",
    };
    const boden = 140;
    const spieler = { x: 60, y: boden, vy: 0, duckt: false };
    let hindernisse = [];
    const startTempo = studio ? 250 : 300;
    const zielTempo = startTempo + (studio ? 210 : 320);   // so schnell am Ende der Runde
    let tempo = startTempo;
    let anteil = 0;      // Fortschritt der Laufrunde 0..1
    let geschafft = false;
    let abstand = 0;
    let leben = 3;
    let schutz = 0;
    let zeit = 0;
    let strecke = 0;
    let lauf = 0;
    let wolken = [{ x: 120, y: 30 }, { x: 380, y: 50 }];

    function rechteck(o) {
      if (o.typ === "vogel") return { x: o.x, y: o.y - 18, w: 34, h: 18 };
      return { x: o.x, y: boden - o.h, w: o.w, h: o.h };
    }
    function spielerBox() {
      const h = spieler.duckt && spieler.y >= boden ? 26 : 44;
      return { x: spieler.x + 4, y: spieler.y - h, w: 30, h };
    }
    function trifft(a, b) {
      return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
    }

    function zeichneLaeufer() {
      const x = spieler.x, y = spieler.y;
      const blink = schutz > 0 && Math.floor(schutz * 10) % 2 === 0;
      if (blink) return;
      ctx.fillStyle = farbe.text;
      if (spieler.duckt && y >= boden) {
        ctx.fillRect(x, y - 24, 40, 16);        // Körper flach
        ctx.fillRect(x + 30, y - 30, 12, 10);   // Kopf
        const b = Math.floor(lauf * 12) % 2;
        ctx.fillRect(x + 6 + b * 8, y - 8, 6, 8);
        ctx.fillRect(x + 22 - b * 8, y - 8, 6, 8);
        return;
      }
      ctx.fillRect(x + 12, y - 44, 14, 14);     // Kopf
      ctx.fillRect(x + 10, y - 30, 18, 18);     // Körper
      ctx.fillStyle = farbe.akzent;
      ctx.fillRect(x + 10, y - 30, 18, 4);      // Stirnband-Akzent am Shirt
      ctx.fillStyle = farbe.text;
      const b = y < boden ? 0 : Math.floor(lauf * 12) % 2;
      ctx.fillRect(x + 10 + b * 6, y - 12, 6, 12);
      ctx.fillRect(x + 22 - b * 6, y - 12, 6, 12);
    }

    function zeichneHindernis(o) {
      const r = rechteck(o);
      if (o.typ === "vogel") {
        ctx.fillStyle = farbe.gefahr;
        const flug = Math.floor(zeit * 8) % 2;
        ctx.fillRect(r.x + 8, r.y + 6, 20, 8);
        ctx.fillRect(r.x + (flug ? 10 : 12), r.y + (flug ? 0 : 12), 12, 6);
        ctx.fillRect(r.x + 26, r.y + 4, 8, 6);
        return;
      }
      ctx.fillStyle = farbe.akzent;
      ctx.fillRect(r.x + r.w / 2 - 5, r.y, 10, r.h);
      ctx.fillRect(r.x, r.y + r.h * 0.35, r.w, 6);
      ctx.fillRect(r.x, r.y + r.h * 0.2, 5, r.h * 0.2);
      ctx.fillRect(r.x + r.w - 5, r.y + r.h * 0.25, 5, r.h * 0.15);
    }

    return {
      fertig: false,
      update(dt) {
        zeit += dt;
        strecke += tempo * dt / 10;
        lauf += dt;
        // Mit Laufstrecke wird es mit jedem Meter der Runde schwerer, sonst mit der Zeit.
        if (runde) tempo += (startTempo + (zielTempo - startTempo) * anteil - tempo) * Math.min(1, dt * 2);
        else {
          // Auf Zeit: bis zum Ende so schnell wie zielTempo, egal wie lang das Training ist.
          const ziel = startTempo + (zielTempo - startTempo) * Math.min(1, zeit / dauer);
          tempo += (ziel - tempo) * Math.min(1, dt * 2);
        }
        schutz = Math.max(0, schutz - dt);

        spieler.vy += 2300 * dt;
        spieler.y = Math.min(boden, spieler.y + spieler.vy * dt);
        if (spieler.y >= boden) spieler.vy = 0;

        abstand -= tempo * dt;
        if (abstand <= 0) {
          const spaet = runde ? anteil > (studio ? 0.35 : 0.2) : zeit / dauer > (studio ? 0.35 : 0.2);
          const vogel = spaet && Math.random() < (studio ? 0.25 : 0.35);
          if (vogel) hindernisse.push({ typ: "vogel", x: cv.width + 10, y: boden - 34 - Math.round(Math.random()) * 26 });
          else hindernisse.push({ typ: "kaktus", x: cv.width + 10, w: 18 + Math.random() * 16, h: 26 + Math.random() * 22 });
          abstand = 230 + Math.random() * 240 + tempo * 0.3;
        }
        for (const o of hindernisse) o.x -= tempo * dt;
        hindernisse = hindernisse.filter((o) => o.x > -60);

        if (schutz <= 0) {
          const sb = spielerBox();
          for (const o of hindernisse) {
            if (trifft(sb, rechteck(o))) {
              leben--;
              schutz = 1.2;
              // Der Charakter stolpert im Spiel mit, je weniger Leben, desto heftiger.
              Nui.post("treffer", { leben });
              hindernisse = hindernisse.filter((x) => x !== o);
              break;
            }
          }
        }
        if (leben <= 0) {
          this.verloren = true;
          this.fertig = true;
        }

        for (const w of wolken) {
          w.x -= tempo * 0.15 * dt;
          if (w.x < -60) { w.x = cv.width + 20; w.y = 20 + Math.random() * 40; }
        }

        ctx.clearRect(0, 0, cv.width, cv.height);
        ctx.fillStyle = farbe.dim;
        for (const w of wolken) { ctx.fillRect(w.x, w.y, 40, 6); ctx.fillRect(w.x + 8, w.y - 6, 22, 6); }
        ctx.fillStyle = farbe.linie;
        ctx.fillRect(0, boden, cv.width, 2);
        for (let i = 0; i < 14; i++) {
          const x = (i * 47 - (zeit * tempo) % 47 + 47) % (cv.width + 47) - 20;
          ctx.fillRect(x, boden + 8 + (i % 3) * 5, 10, 2);
        }
        for (const o of hindernisse) zeichneHindernis(o);
        zeichneLaeufer();

        ctx.fillStyle = farbe.gefahr;
        for (let i = 0; i < 3; i++) {
          ctx.globalAlpha = i < leben ? 1 : 0.2;
          ctx.fillRect(cv.width - 28 - i * 22, 14, 14, 14);
        }
        ctx.globalAlpha = 1;
        ctx.fillStyle = farbe.text;
        ctx.font = "700 14px Segoe UI, Arial";
        ctx.fillText(Math.floor(zeit * tempo / 10) + " m", 14, 26);
      },
      taste(e, runter) {
        if ((e.key === " " || e.key === "ArrowUp" || e.key === "w") && runter && spieler.y >= boden && !spieler.duckt) spieler.vy = -720;
        if (e.key === "ArrowDown" || e.key === "s") spieler.duckt = runter;
      },
      ohneZeit: runde,
      fortschritt(w) {
        anteil = Math.max(anteil, Math.min(1, w));
      },
      rundeFertig() {
        anteil = 1;
        geschafft = true;
        this.fertig = true;
      },
      anzeige: runde ? () => Math.round(anteil * 100) + " %" : null,
      punkte() {
        const basis = runde ? (geschafft ? 100 : anteil * 100) : Math.min(1, zeit / dauer) * 100;
        return basis - (3 - leben) * 15;
      },
      rekord() {
        return Math.round(strecke); // gelaufene Meter (grob)
      },
    };
  },
};

// ---------------------------------------------- Flexibilität: Balance halten
SPIELE.balance = {
  dauer: 30,
  hilfe: "<kbd>Leertaste</kbd> gedrückt halten schiebt nach rechts, loslassen kippt nach links. Nur im grünen Bereich füllt sich der Balken.",
  neu(studio, runde, dauer) {
    dauer = dauer || SPIELE.balance.dauer;
    const zone = $("balance-zone");
    const nadel = $("balance-nadel");
    const quote = $("balance-quote");
    let p = 0.5, v = 0;
    let halten = false;
    let zeit = 0, drin = 0;
    let wind = 0;
    const startBreite = studio ? 0.36 : 0.26;
    const endBreite = studio ? 0.22 : 0.13;

    return {
      fertig: false,
      update(dt) {
        zeit += dt;
        const anteil = Math.min(1, zeit / dauer);
        const breite = startBreite + (endBreite - startBreite) * anteil;
        const mitte = 0.5 + Math.sin(zeit * 0.55) * 0.22 + Math.sin(zeit * 1.37 + 1) * 0.08 * (studio ? 0.8 : 1);

        if (Math.random() < dt * 0.6) wind = (Math.random() - 0.5) * (studio ? 0.6 : 1.1);
        wind *= Math.pow(0.4, dt);

        v += ((halten ? 1.5 : -1.3) + wind) * dt;
        v *= Math.pow(0.35, dt);
        p += v * dt;
        if (p < 0) { p = 0; v = 0; }
        if (p > 1) { p = 1; v = 0; }

        const istDrin = Math.abs(p - mitte) <= breite / 2;
        if (istDrin) drin += dt;

        zone.style.left = (mitte - breite / 2) * 100 + "%";
        zone.style.width = breite * 100 + "%";
        zone.classList.toggle("is-drin", istDrin);
        nadel.style.left = p * 100 + "%";
        // Fortschritt wächst nur im grünen Bereich und ist voll, wenn man rund 87 % der Zeit drin war.
        quote.style.setProperty("--value", Math.min(100, (drin * 1.15 / dauer) * 100) + "%");
      },
      taste(e, runter) {
        if (e.key === " ") halten = runter;
      },
      punkte() {
        return (drin / dauer) * 115;
      },
      rekord() {
        return zeit > 0 ? Math.round((drin / zeit) * 100) : 0;
      },
    };
  },
};

// ---------------------------------------------- Flexibilität: Yoga wie im Original-GTA
// Pro Haltung zwei Schritte: erst beide "Sticks" in die gezeigte Richtung bringen (WASD links, Pfeiltasten rechts)
// und kurz ruhig halten, dann im Takt atmen (Leertaste halten beim Einatmen, loslassen beim Ausatmen).
SPIELE.yoga = {
  dauer: 75,
  hilfe: "Richte <kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> und die <kbd>Pfeiltasten</kbd> auf die Markierungen aus und halte still. Dann atmen: <kbd>Leertaste</kbd> halten, solange der Kreis wächst, loslassen, wenn er schrumpft.",
  neu(studio, runde, dauer) {
    dauer = dauer || SPIELE.yoga.dauer;
    const RICHTUNGEN = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];
    // Eine Haltung dauert höchstens etwa 21 Sekunden (Ausrichten und drei Atemzüge).
    const haltungen = Math.max(3, Math.round(dauer / 22));
    const gedrueckt = new Set();
    const stickL = { feld: $("yoga-l"), punkt: $("yoga-l-punkt"), ziel: $("yoga-l-ziel"), x: 0, y: 0, soll: 0 };
    const stickR = { feld: $("yoga-r"), punkt: $("yoga-r-punkt"), ziel: $("yoga-r-ziel"), x: 0, y: 0, soll: 0 };
    const kreis = $("yoga-atem");
    const text = $("yoga-text");
    const stand = $("yoga-stand");

    let nr = 0;
    let phase = "";          // "haltung" oder "atmen"
    let zeit = 0;            // in der aktuellen Phase
    let ruhig = 0;           // wie lange beide Richtungen stimmen
    let atemDrin = 0, atemZeit = 0;
    let summe = 0;
    let halten = false;

    const haltungsZeit = studio ? 12 : 9;     // so lange darf das Ausrichten dauern
    const ruhigNoetig = studio ? 0.6 : 0.9;
    const atemzuege = 3;

    function zielSetzen(stick, nurGerade) {
      let r;
      do r = Math.floor(Math.random() * 8); while ((nurGerade && r % 2) || r === stick.soll);
      stick.soll = r;
      const [x, y] = RICHTUNGEN[r];
      const l = Math.hypot(x, y);
      stick.ziel.style.left = 50 + (x / l) * 38 + "%";
      stick.ziel.style.top = 50 + (y / l) * 38 + "%";
    }

    function naechsteHaltung() {
      nr++;
      phase = "haltung";
      zeit = 0;
      ruhig = 0;
      // Die erste Haltung nur mit geraden Richtungen, danach auch schräg (bei Studio immer gerade).
      const gerade = studio || nr === 1;
      zielSetzen(stickL, gerade);
      zielSetzen(stickR, gerade);
      stand.textContent = `Haltung ${nr} / ${haltungen}`;
      text.textContent = "Haltung finden";
      kreis.className = "fx-atem";
      kreis.style.transform = "scale(0.55)";
    }

    function stickWert(stick, hoch, runter, links, rechts, dt) {
      const zx = (gedrueckt.has(rechts) ? 1 : 0) - (gedrueckt.has(links) ? 1 : 0);
      const zy = (gedrueckt.has(runter) ? 1 : 0) - (gedrueckt.has(hoch) ? 1 : 0);
      const l = Math.hypot(zx, zy) || 1;
      const f = 1 - Math.pow(0.0005, dt);
      stick.x += (zx / l - stick.x) * f;
      stick.y += (zy / l - stick.y) * f;
      stick.punkt.style.left = 50 + stick.x * 38 + "%";
      stick.punkt.style.top = 50 + stick.y * 38 + "%";
      const [sx, sy] = RICHTUNGEN[stick.soll];
      const stimmt = zx === sx && zy === sy;
      stick.feld.classList.toggle("is-drin", stimmt);
      return stimmt;
    }

    naechsteHaltung();

    return {
      fertig: false,
      update(dt) {
        zeit += dt;
        const l = stickWert(stickL, "w", "s", "a", "d", dt);
        const r = stickWert(stickR, "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", dt);

        if (phase === "haltung") {
          ruhig = l && r ? ruhig + dt : 0;
          text.textContent = l && r ? "Jetzt halten" : "Haltung finden";
          if (ruhig >= ruhigNoetig || zeit >= haltungsZeit) {
            // Schnell gefunden gibt bis zu 40 Punkte, nicht gefunden 0.
            const gefunden = ruhig >= ruhigNoetig;
            summe += gefunden ? 40 * Math.max(0.35, 1 - Math.max(0, zeit - 2) / (haltungsZeit - 2)) : 0;
            phase = "atmen";
            zeit = 0;
            atemDrin = 0;
            atemZeit = 0;
            text.textContent = gefunden ? "Atmen" : "Haltung verpasst, trotzdem atmen";
          }
          return;
        }

        // Atmen: ein Atemzug dauert bei jeder Haltung etwas kürzer.
        const anteil = haltungen > 1 ? (nr - 1) / (haltungen - 1) : 0;
        const atemzug = (studio ? 5.2 : 4.6) - anteil * (studio ? 0.9 : 1.4);
        const t = zeit / atemzug;
        if (t >= atemzuege) {
          summe += 60 * (atemZeit > 0 ? Math.min(1, (atemDrin / atemZeit) * 1.1) : 0);
          if (nr >= haltungen) this.fertig = true;
          else naechsteHaltung();
          return;
        }
        const teil = t % 1;
        const einatmen = teil < 0.5;
        const groesse = 0.55 + 0.45 * Math.sin(Math.PI * teil);
        kreis.style.transform = `scale(${groesse.toFixed(3)})`;
        // Kurz nach jedem Wechsel zählt noch nicht, damit man reagieren kann.
        if ((teil % 0.5) > 0.08) {
          atemZeit += dt;
          const richtig = halten === einatmen;
          if (richtig) atemDrin += dt;
          kreis.className = "fx-atem " + (richtig ? "is-richtig" : "is-falsch");
        }
        text.textContent = (einatmen ? "Einatmen" : "Ausatmen") + ` · ${Math.floor(t) + 1} / ${atemzuege}`;
      },
      taste(e, runter) {
        const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
        if (k === " ") { halten = runter; return; }
        if (runter) gedrueckt.add(k); else gedrueckt.delete(k);
      },
      punkte() {
        return summe / haltungen;
      },
      rekord() {
        return Math.round(summe / haltungen);
      },
    };
  },
};

// ---------------------------------------------- Kondition: Rhythmus-Tasten
SPIELE.rhythmus = {
  dauer: 45,
  hilfe: "Drück die angezeigte Taste <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd>, bevor der Ring leer ist. Es wird immer schneller.",
  neu(studio, runde, dauer) {
    dauer = dauer || SPIELE.rhythmus.dauer;
    const tasten = ["W", "A", "S", "D"];
    const pfeile = { ArrowUp: "W", ArrowLeft: "A", ArrowDown: "S", ArrowRight: "D" };
    // Etwa eine Taste alle 1,75 Sekunden, so schafft man alle Tasten, bevor die Zeit um ist.
    const gesamt = Math.max(24, Math.round(dauer / 1.75));
    const feld = $("rhythmus-taste");
    const ring = $("rhythmus-ring");
    const stand = $("rhythmus-stand");
    const combo = $("rhythmus-combo");
    let nr = 0, treffer = 0, serie = 0;
    let aktuell = "";
    let fenster = 1.5, uebrig = 0;
    let pause = 0.6;

    function naechste() {
      nr++;
      let t;
      do t = tasten[Math.floor(Math.random() * 4)]; while (t === aktuell && Math.random() < 0.7);
      aktuell = t;
      const fortschritt = nr / gesamt;
      fenster = (studio ? 1.7 : 1.5) - fortschritt * (studio ? 0.55 : 0.85);
      uebrig = fenster;
      feld.textContent = aktuell;
      feld.classList.remove("is-treffer", "is-fehler");
    }
    function werten(ok) {
      if (ok) { treffer++; serie++; } else serie = 0;
      feld.classList.add(ok ? "is-treffer" : "is-fehler");
      aktuell = "";
      pause = 0.35;
      stand.textContent = `${treffer} / ${nr}`;
      combo.textContent = serie >= 3 ? `${serie}er Serie` : "";
    }

    stand.textContent = "0 / 0";
    combo.textContent = "";
    feld.textContent = "·";
    feld.classList.remove("is-treffer", "is-fehler");

    return {
      fertig: false,
      update(dt) {
        if (aktuell) {
          uebrig -= dt;
          ring.style.strokeDashoffset = 276.5 * (1 - Math.max(0, uebrig) / fenster);
          if (uebrig <= 0) werten(false);
        } else {
          pause -= dt;
          ring.style.strokeDashoffset = 276.5;
          if (pause <= 0) {
            if (nr >= gesamt) this.fertig = true;
            else naechste();
          }
        }
      },
      taste(e, runter) {
        if (!runter || e.repeat || !aktuell) return;
        const t = pfeile[e.key] || e.key.toUpperCase();
        if (!tasten.includes(t)) return;
        werten(t === aktuell);
      },
      punkte() {
        return (treffer / gesamt) * 100;
      },
      rekord() {
        return treffer;
      },
    };
  },
};

// Vorschau im normalen Browser: ?spiel=kraft|dino|balance|yoga|rhythmus oder ohne Parameter die Übersicht
if (!Nui.inGame) {
  const name = new URLSearchParams(location.search).get("spiel");
  const karte = new URLSearchParams(location.search).get("karte");
  setTimeout(() => {
    window.postMessage({ action: "adminDaten", art: "kurse", daten: [
      { id: 1, titel: "Yoga am Strand", gym: "Muscle Beach Gym", uebung: "Yoga-Kurs", zeit: "Heute 20:00", leiter: "Lena Vogt", teilnehmer: 7, max: 10, laeuft: false },
      { id: 2, titel: "Lauftreff", gym: "Iron Paradise", uebung: "Lauf-Kurs", zeit: "Morgen 18:30", leiter: "Jonas Becker", teilnehmer: 3, max: 15, laeuft: false }] }, "*");
    window.postMessage({ action: "adminDaten", art: "besten", daten: { uebung: "kraft:frei", einheit: "Wdh.", liste: [
      { cid: "A1", name: "Lena Vogt", wert: 48, datum: "09.11.2026" }, { cid: "B2", name: "Max Muster", wert: 41, datum: "10.11.2026" }] } }, "*");
  }, 400);
  setTimeout(() => window.postMessage({ action: "spielerWerte", id: 1, name: "toter", mitglied: true, kater: "14:30", shake: "13:10", vitamine: "",
    probetage: [{ id: 2, name: "Iron Paradise" }], werte: [
    { stat: "kraft", name: "Kraft", wert: 62, heute: true, zuletzt: "06.11.2026 16:40" },
    { stat: "ausdauer", name: "Ausdauer", wert: 48, heute: false, zuletzt: "05.11.2026 21:10" },
    { stat: "flexibilitaet", name: "Flexibilität", wert: 35, heute: false, zuletzt: "nie" },
    { stat: "kondition", name: "Kondition", wert: 71, heute: true, zuletzt: "06.11.2026 16:55" } ] }, "*"), 300);
  const empf = new URLSearchParams(location.search).get("empfang");
  if (empf !== null) {
    window.postMessage({ action: "empfang", gymId: 1, gym: "Muscle Beach Gym", modus: empf || "verkauf", meineKarte: "06.12.2026 11:40",
      tarife: [{ id: "tag", name: "1 Tag", preis: 100 }, { id: "woche", name: "7 Tage", preis: 500 }, { id: "monat", name: "30 Tage", preis: 1500 }],
      kunden: [{ id: 4, name: "Alex Beispiel" }],
      shop: [{ item: "proteinshake", name: "Protein-Shake", preis: 80, text: "2 Stunden lang +1 Punkt pro Übung." },
             { item: "vitamine", name: "Vitamin-Kapseln", preis: 120, text: "24 Stunden lang bauen deine Fitness-Werte nicht ab." }] }, "*");
    if (empf === "angebot") window.postMessage({ action: "angebot", titel: "Muscle Beach Gym", text: "Mitgliedschaft 30 Tage", art: "mitgliedschaft", preis: 1500, verkaeufer: "Max Muster", sekunden: 60 }, "*");
  } else if (karte !== null) {
    window.postMessage({ action: "karte", eigene: true, gym: karte || "Muscle Beach Gym", name: "Alex Beispiel", geburtstag: "01.01.1990", nummer: "01-4821-7730", ablauf: "06.12.2026 11:40", restTage: 30 }, "*");
  } else if (name && SPIELE[name]) {
    window.postMessage({ action: "spiel", spiel: name, titel: "Vorschau", stat: name, studio: false }, "*");
  } else {
    window.postMessage({
      action: "menue", oeffnen: true, admin: true, preis: 50, meineId: 1, koerper: "normal",
      stats: { kraft: 62, ausdauer: 48, flexibilitaet: 35, kondition: 71 }, einheiten: 12,
      stationen: [
        { id: 3, name: "Klimmzüge", stat: "kraft", studio: false, abstand: 4, route: 0, joggen: false, prop: "prop_beach_bars_01", hoehe: 0 },
        { id: 7, name: "Joggen", stat: "ausdauer", studio: false, abstand: 12, route: 6, joggen: true, prop: "", hoehe: 0 },
        { id: 9, name: "Laufband", stat: "ausdauer", studio: true, abstand: 38, route: 0, joggen: false, prop: "apa_p_apdlc_treadmill_s", hoehe: 0.1, gymId: 1, gym: "Muscle Beach Gym" },
      ],
      supplements: [{ item: "proteinshake", name: "Protein-Shake" }, { item: "vitamine", name: "Vitamin-Kapseln" }, { item: "muskelbalsam", name: "Muskelbalsam" }],
      gyms: [{ id: 1, name: "Muscle Beach Gym", stationen: 3, job: "gym", tag: 100, woche: 500, monat: 1500, empfang: true, shop: { proteinshake: 80, vitamine: 120, muskelbalsam: 60 } }, { id: 2, name: "Iron Paradise", stationen: 0, job: "", tag: 80, woche: 400, monat: 1200, empfang: false }],
      karten: [{ gym: "Muscle Beach Gym", bis: "06.12.2026 11:40" }],
      muskelkater: "07.11.2026 16:00", shake: "06.11.2026 19:30", trainer: true,
      chef: [{ id: 1, name: "Muscle Beach Gym", tag: 100, woche: 500, monat: 1500, empfang: true,
        shop: [{ item: "proteinshake", name: "Protein-Shake", preis: 80 }, { item: "vitamine", name: "Vitamin-Kapseln", preis: 120 }, { item: "muskelbalsam", name: "Muskelbalsam", preis: 60 }],
        mitarbeiter: [{ name: "Lena Vogt", rang: "Trainerin", dienst: true }, { name: "Jonas Becker", rang: "Empfang", dienst: false }] }],
      trainerDaten: { preis: 150, kunden: [{ id: 5, name: "Max Muster", status: "meine", einheiten: 2, bis: "22:40", uebung: "Bankdrücken" }, { id: 6, name: "Anni", status: "frei", einheiten: 0, bis: "", uebung: "" }] },
      uebungenAlle: [{ id: "kraft:frei", name: "Klimmzüge", stat: "kraft" }, { id: "kraft:studio", name: "Bankdrücken (Studio)", stat: "kraft" }],
      uebungen: [
        { stat: "kraft", art: "frei", name: "Klimmzüge", prop: "prop_beach_bars_01" },
        { stat: "ausdauer", art: "frei", name: "Joggen", prop: "" },
      ],
    }, "*");
  }
}
