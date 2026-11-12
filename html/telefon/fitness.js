const STATS = [
  { id: "kraft", name: "Kraft", farbe: "var(--kraft)", icon: '<path d="M6.5 6.5v11M17.5 6.5v11M3 9v6M21 9v6M6.5 12h11"/>' },
  { id: "ausdauer", name: "Ausdauer", farbe: "var(--ausdauer)", icon: '<path d="M13 4a2 2 0 1 0 0-.01M9 21l3-6 3 2v5M6 12l3-4 4 1 3 3 3 1"/>' },
  { id: "flexibilitaet", name: "Flexibilität", farbe: "var(--flex)", icon: '<path d="M12 4a2 2 0 1 0 0-.01M4 10c3 1 5 1 8 1s5 0 8-1M12 11v4l-4 6M12 15l4 6"/>' },
  { id: "kondition", name: "Kondition", farbe: "var(--kondition)", icon: '<path d="M12 21s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 11c0 5.6-7 10-7 10z"/><path d="M8 12h2l1-2 2 4 1-2h2"/>' },
];





function stufe(wert, d) {
  if (wert < d.negativUnter) return "Schwach, das merkst du im Alltag";
  if (wert < d.positivAb) return "Normal";
  if (wert < d.vollAb) return "Gut trainiert, erste Vorteile";
  return "Topform, volle Vorteile";
}

function ringe(d) {
  const box = $("ringe");
  box.textContent = "";
  const ns = "http://www.w3.org/2000/svg";
  STATS.forEach((s, i) => {
    const r = 54 - i * 12;
    const umfang = 2 * Math.PI * r;
    const wert = Math.max(0, Math.min(100, d.stats[s.id] || 0));
    for (const art of ["spur", "wert"]) {
      const c = document.createElementNS(ns, "circle");
      c.setAttribute("cx", 60);
      c.setAttribute("cy", 60);
      c.setAttribute("r", r);
      c.setAttribute("stroke-width", 10);
      c.style.stroke = s.farbe;
      if (art === "spur") c.style.opacity = 0.22;
      else {
        c.classList.add("hx-ring-wert");
        c.style.strokeDasharray = umfang;
        c.style.strokeDashoffset = umfang;
        requestAnimationFrame(() => requestAnimationFrame(() => (c.style.strokeDashoffset = umfang * (1 - wert / 100))));
      }
      box.append(c);
    }
  });

  const leg = $("legende");
  leg.textContent = "";
  for (const s of STATS) {
    const li = document.createElement("li");
    li.style.setProperty("--c", s.farbe);
    li.append(text("span", "", s.name));
    const b = text("b", "", d.stats[s.id] || 0);
    li.append(b, text("small", "", "/100" + ((d.heute || []).includes(s.id) ? " · heute ✓" : "")));
    leg.append(li);
  }
  $("reset").textContent = `neuer Tag ab ${String(d.reset).padStart(2, "0")}:00`;
}

function werte(d) {
  const box = $("werte");
  box.textContent = "";
  for (const s of STATS) {
    const wert = d.stats[s.id] || 0;
    const heute = (d.heute || []).includes(s.id);
    const karte = el("section", "hx-karte hx-wert");
    const kopf = el("div", "hx-karte-kopf");
    const tag = el("span", "hx-tag", svg(s.icon));
    tag.style.setProperty("--c", s.farbe);
    tag.append(s.name);
    const zeit = text("span", "hx-zeit" + (heute ? " is-heute" : ""), heute ? "Heute trainiert" : "Heute noch offen");
    kopf.append(tag, zeit);
    const zahl = el("div", "hx-zahl");
    zahl.append(text("b", "", wert), text("small", "", "von 100"));
    const balken = el("div", "hx-balken");
    balken.style.setProperty("--c", s.farbe);
    const fill = el("span");
    balken.append(fill);
    for (const grenze of [d.negativUnter, d.positivAb, d.vollAb]) {
      const strich = el("i");
      strich.style.left = grenze + "%";
      balken.append(strich);
    }
    requestAnimationFrame(() => requestAnimationFrame(() => (fill.style.width = wert + "%")));
    zeit.append(el("i", "hx-pfeil", "›"));
    karte.append(kopf, zahl, text("div", "hx-stufe", stufe(wert, d)), balken);
    karte.classList.add("is-klickbar");
    karte.onclick = () => verlaufZeigen(s, d);
    box.append(karte);
  }
}

function zeile(farbe, icon, titel, unter, rechts) {
  const z = el("div", "hx-zeile");
  const p = el("span", "hx-punkt", svg(icon));
  p.style.setProperty("--c", farbe);
  const t = el("span");
  t.append(document.createTextNode(titel));
  if (unter) t.append(text("small", "", unter));
  z.append(p, t);
  if (rechts) z.append(text("b", "", rechts));
  return z;
}

function zustand(d) {
  const box = $("zustand");
  box.textContent = "";
  const uhr = (s) => (s ? s.slice(11, 16) : "");
  const tag = (s) => (s ? s.slice(0, 5) : "");
  if (d.muskelkater) box.append(zeile("var(--kraft)", '<path d="M4 14c4-6 12-6 16 0"/><path d="M12 8v4"/>', "Muskelkater", "Du bist etwas langsamer. Muskelbalsam hilft.", `bis ${uhr(d.muskelkater)}`));
  else if (d.muskelkaterMorgen) box.append(zeile("var(--kondition)", '<path d="M12 8v5l3 2"/><circle cx="12" cy="12" r="9"/>', "Muskelkater kommt", "Alle vier Werte heute trainiert.", `ab ${tag(d.muskelkaterMorgen)}`));
  if (d.shake) box.append(zeile("var(--lila)", '<path d="M7 4h10l-1.5 16h-7z"/><path d="M7.5 9h9"/>', "Protein-Shake", "+1 pro Übung", `bis ${uhr(d.shake)}`));
  if (d.vitamine) box.append(zeile("var(--ausdauer)", '<rect x="4" y="9" width="16" height="7" rx="3.5"/><path d="M12 9v7"/>', "Vitamine", "Deine Werte sinken gerade nicht.", `bis ${tag(d.vitamine)}`));
  if (!box.children.length) box.append(text("div", "hx-leer", "Alles im grünen Bereich."));
}

function mitglied(d) {
  const box = $("mitglied");
  box.textContent = "";
  const karten = d.karten || [];
  if (!karten.length) box.append(text("div", "hx-leer", "Keine aktive Mitgliedschaft. Ohne Mitgliedschaft trainierst du an freien Stationen."));
  for (const k of karten) box.append(zeile("var(--flex)", '<rect x="3" y="6" width="18" height="12" rx="2"/><path d="M3 10h18"/>', k.gym, "gültig bis " + k.bis.slice(0, 10)));
}


function stationen(d) {
  const box = $("stationen");
  box.textContent = "";
  const liste = d.stationen || [];
  if (!liste.length) box.append(text("div", "hx-leer", "Keine Station in der Nähe."));
  for (const s of liste) {
    const st = STATS.find((x) => x.id === s.stat) || STATS[0];
    const z = zeile(st.farbe, st.icon, s.name, (s.studio ? (s.gym || "Studio") + (s.gym ? "" : ` · ${geld(d.preis)}`) : "kostenlos") + ` · ${s.abstand} m`);
    const k = text("button", "hx-knopf", "Route");
    k.type = "button";
    k.onclick = async () => {
      k.textContent = "✓";
      await nui("appWegpunkt", { id: s.id });
      setTimeout(() => (k.textContent = "Route"), 1500);
    };
    z.append(k);
    box.append(z);
  }
}

function bestenAuswahl(d) {
  const wahl = $("rekord-wahl");
  if (wahl.options.length) return;
  for (const u of d.uebungen || []) {
    const o = text("option", "", u.name);
    o.value = u.id;
    wahl.append(o);
  }
  besten();
}

async function besten() {
  const box = $("rekorde");
  const d = await nui("appBestenliste", { uebung: $("rekord-wahl").value });
  if (!d || !d.liste) {
    if (imSpiel) box.replaceChildren(text("div", "hx-leer", "Bestenliste gerade nicht erreichbar."));
    return;
  }
  bestenZeigen(d);
}

function bestenZeigen(d) {
  const box = $("rekorde");
  box.textContent = "";
  if (!d.liste.length) box.append(text("div", "hx-leer", "Noch keine Rekorde."));
  d.liste.forEach((e, i) => {
    const z = el("div", "hx-zeile" + (e.ich ? " is-ich" : ""));
    z.append(text("span", "hx-platz", i + 1));
    const t = el("span");
    t.append(document.createTextNode(e.name));
    t.append(text("small", "", e.datum));
    z.append(t, text("b", "", `${e.wert} ${d.einheit}`));
    box.append(z);
  });
  $("rekord-eigen").textContent = d.eigen ? `Dein Rekord: ${d.eigen} ${d.einheit}` : "";
}

$("rekord-wahl").addEventListener("change", besten);

async function verlaufZeigen(s, d) {
  const wert = d.stats[s.id] || 0;
  $("detail").style.setProperty("--c", s.farbe);
  $("detail-name").textContent = s.name;
  $("detail-wert").textContent = wert;
  $("detail-stufe").textContent = stufe(wert, d);
  $("detail-zeitraum").textContent = "Letzte 30 Tage";
  kurveZeichnen([], d);
  $("detail-zahlen").textContent = "";
  $("detail").hidden = false;
  $("app").hidden = true;

  let v = await nui("appVerlauf", { stat: s.id });
  if (!imSpiel) v = beispielVerlauf(wert);
  const punkte = (v && v.punkte) || [];
  if (!punkte.length || punkte[punkte.length - 1].wert !== wert) punkte.push({ tag: "heute", wert });
  kurveZeichnen(punkte, d);
  const werteListe = punkte.map((p) => p.wert);
  const veraenderung = werteListe[werteListe.length - 1] - werteListe[0];
  const box = $("detail-zahlen");
  box.textContent = "";
  for (const [titel, inhalt] of [
    ["Höchster Wert", Math.max(...werteListe)],
    ["Tiefster Wert", Math.min(...werteListe)],
    ["Veränderung", (veraenderung > 0 ? "+" : "") + veraenderung],
    ["Heute trainiert", (d.heute || []).includes(s.id) ? "Ja" : "Nein"],
  ]) {
    const z = el("div", "hx-zeile");
    z.append(text("span", "", titel), text("b", "", inhalt));
    box.append(z);
  }
}

function kurveZeichnen(punkte, d) {
  const box = $("kurve");
  const B = 320;
  const H = 170;
  const y = (w) => H - 8 - (w / 100) * (H - 16);
  let html = "";
  for (const g of [d.negativUnter, d.positivAb, d.vollAb]) html += `<line class="hx-gitter" x1="0" x2="${B}" y1="${y(g)}" y2="${y(g)}"/>`;
  if (punkte.length) {
    const x = (i) => (punkte.length === 1 ? B / 2 : 6 + (i / (punkte.length - 1)) * (B - 12));
    const linie = punkte.map((p, i) => `${i ? "L" : "M"}${x(i).toFixed(1)},${y(p.wert).toFixed(1)}`).join(" ");
    html += `<path class="hx-flaeche" d="${linie} L${x(punkte.length - 1).toFixed(1)},${H} L${x(0).toFixed(1)},${H} Z"/>`;
    html += `<path class="hx-linie" d="${linie}"/>`;
    const l = punkte.length - 1;
    html += `<circle class="hx-punkt-ende" cx="${x(l)}" cy="${y(punkte[l].wert)}" r="4.5"/>`;
    $("achse-von").textContent = punkte[0].tag;
    $("achse-bis").textContent = punkte[l].tag === "heute" ? "Heute" : punkte[l].tag;
  } else {
    $("achse-von").textContent = "";
    $("achse-bis").textContent = "";
  }
  box.innerHTML = html;
}

function beispielVerlauf(jetzt) {
  const punkte = [];
  let w = Math.max(20, jetzt - 18);
  for (let i = 0; i < 30; i++) {
    w = Math.max(0, Math.min(100, w + (i % 4 === 3 ? -1 : Math.random() < 0.6 ? 1 : 0)));
    punkte.push({ tag: `${String(((i + 11) % 30) + 1).padStart(2, "0")}.10`, wert: w });
  }
  return { punkte };
}

function zurueck() {
  $("detail").hidden = true;
  $("app").hidden = false;
}
$("zurueck").onclick = zurueck;
// Zurück auch mit Rücktaste oder Wischen nach rechts, falls oben etwas vom Handy über dem Knopf liegt.
document.addEventListener("keydown", (e) => {
  if (e.key === "Backspace" && !$("detail").hidden && !/INPUT|SELECT|TEXTAREA/.test(document.activeElement.tagName)) zurueck();
});
let wischStart = null;
$("detail").addEventListener("pointerdown", (e) => (wischStart = { x: e.clientX, y: e.clientY }));
$("detail").addEventListener("pointerup", (e) => {
  if (wischStart && e.clientX - wischStart.x > 80 && Math.abs(e.clientY - wischStart.y) < 60) zurueck();
  wischStart = null;
});

function kurse(d) {
  const box = $("kurse");
  const liste = d.kurse || [];
  $("kurse").hidden = $("kurse-titel").hidden = !liste.length;
  box.textContent = "";
  for (const k of liste) {
    const p = el("span", "hx-punkt", svg('<path d="M12 4a2 2 0 1 0 0-.01M4 10c3 1 5 1 8 1s5 0 8-1M12 11v4l-4 6M12 15l4 6"/>'));
    p.style.setProperty("--c", k.laeuft ? "var(--ausdauer)" : "var(--flex)");
    const voll = !k.angemeldet && k.teilnehmer >= k.max;
    const an = knopf(k.angemeldet ? "Abmelden" : voll ? "Ausgebucht" : "Anmelden", !k.angemeldet && !voll, async (b) => {
      if (k.angemeldet && !(await frage("Vom Kurs abmelden", k.titel, "Abmelden", true))) return;
      b.disabled = true;
      const r = await nui("appKurs", { aktion: k.angemeldet ? "abmelden" : "anmelden", id: k.id });
      if (r && r.kurse) kurse({ kurse: r.kurse });
    });
    if (voll) an.disabled = true;
    const route = knopf("Route", false, async (b) => {
      b.textContent = "✓";
      await nui("appKurs", { aktion: "route", id: k.id });
      setTimeout(() => (b.textContent = "Route"), 1500);
    });
    box.append(eintrag({
      symbol: p,
      titel: k.titel,
      unter: `${k.uebung} · ${k.laeuft ? "läuft gerade" : k.zeit} · ${k.dauer} Min.`,
      marke: k.laeuft ? "läuft" : k.angemeldet ? "angemeldet" : "",
      markeAn: true,
      infos: [["ort", k.ort || "Kursort"], ["person", `${k.leiter} · ${k.gym}`], ["gruppe", `${k.teilnehmer} von ${k.max} Plätzen belegt`], ["preis", preisText(k.preis)]],
      knoepfe: [an, route],
    }));
  }
}

function zeigen(d) {
  datumKopf();
  ringe(d);
  werte(d);
  kurse(d);
  zustand(d);
  mitglied(d);
  stationen(d);
  bestenAuswahl(d);
  $("einheiten").textContent = d.einheiten || 0;
  $("koerper").textContent = { fett: "untrainiert", athletisch: "athletisch", normal: "normal" }[d.koerper] || "normal";
}

async function holen() {
  const d = await nui("appDaten");
  if (d && d.stats) zeigen(d);
}

window.addEventListener("message", (e) => {
  if (e.data === "componentsLoaded") holen();
  else if (e.data && e.data.type === "fitness") zeigen(e.data);
});

holen();

if (!imSpiel) {
  zeigen({
    stats: { kraft: 62, ausdauer: 48, flexibilitaet: 31, kondition: 91 }, heute: ["kraft", "kondition"], einheiten: 27, koerper: "normal",
    muskelkater: "", muskelkaterMorgen: "", shake: "10.11.2026 19:30", vitamine: "11.11.2026 08:00",
    karten: [{ gym: "Muscle Beach Gym", bis: "06.12.2026 11:40" }], reset: 4, negativUnter: 35, positivAb: 65, vollAb: 90, preis: 50,
    stationen: [
      { id: 3, name: "Klimmzüge", stat: "kraft", studio: false, gym: "", abstand: 4 },
      { id: 9, name: "Laufband", stat: "ausdauer", studio: true, gym: "Muscle Beach Gym", abstand: 38 },
      { id: 12, name: "Yoga", stat: "flexibilitaet", studio: false, gym: "", abstand: 61 },
    ],
    uebungen: [{ id: "kraft:frei", name: "Klimmzüge" }, { id: "kraft:studio", name: "Bankdrücken (Studio)" }],
    kurse: [
      { id: 1, titel: "Yoga am Strand", uebung: "Yoga-Kurs", zeit: "Heute 20:00", dauer: 60, gym: "Muscle Beach Gym", ort: "Bay City Ave, Vespucci Beach", preis: 50, leiter: "Lena Vogt", teilnehmer: 4, max: 10, angemeldet: true, laeuft: false },
    ],
  });
  bestenZeigen({ einheit: "Wdh.", eigen: 31, liste: [{ name: "Lena Vogt", wert: 48, datum: "08.11.2026" }, { name: "Marco Rossi", wert: 41, datum: "05.11.2026" }, { name: "Du", wert: 31, datum: "10.11.2026", ich: true }] });
}
