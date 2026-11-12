// Gemeinsame Helfer für die Handy-Apps Fitness und GymDesk.
const $ = (id) => document.getElementById(id);
const RESSOURCE = location.host.startsWith("cfx-nui-") ? location.host.slice(8) : "toter-fitness";
const imSpiel = location.host.startsWith("cfx-nui-") || typeof fetchNui === "function";

const svg = (pfad) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">${pfad}</svg>`;

function el(tag, klasse, html) {
  const e = document.createElement(tag);
  if (klasse) e.className = klasse;
  if (html !== undefined) e.innerHTML = html;
  return e;
}

function text(tag, klasse, t) {
  const e = document.createElement(tag);
  if (klasse) e.className = klasse;
  e.textContent = t;
  return e;
}

// Gebaut als native lb-phone App: lb-phone stellt fetchNui, components (setPopUp) und settings bereit.
// Fehlt das (anderes Handy, Vorschau im Browser), gibt es einen einfachen Ersatz.
const lbPhone = () => typeof fetchNui === "function";

async function nui(name, daten = {}) {
  if (!imSpiel) return null;
  try {
    if (lbPhone()) {
      const r = await fetchNui(name, daten, RESSOURCE);
      return r && typeof r.json === "function" ? await r.json() : r ?? null;
    }
    const r = await fetch(`https://${RESSOURCE}/${name}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(daten) });
    return await r.json();
  } catch {
    return null;
  }
}

// Rückfrage im Stil des Handys (lb-phone Pop-up). Ohne lb-phone geht es direkt weiter.
function frage(titel, beschreibung, ja, gefaehrlich = false) {
  return new Promise((fertig) => {
    try {
      if (globalThis.components && typeof components.setPopUp === "function") {
        components.setPopUp({
          title: titel,
          description: beschreibung,
          buttons: [
            { title: "Abbrechen", color: "blue", cb: () => fertig(false) },
            { title: ja, color: gefaehrlich ? "red" : "blue", cb: () => fertig(true) },
          ],
        });
        return;
      }
    } catch {}
    fertig(true);
  });
}

const ICON = {
  ort: '<path d="M12 21s-6-5.4-6-11a6 6 0 0 1 12 0c0 5.6-6 11-6 11z"/><circle cx="12" cy="10" r="2.2"/>',
  person: '<circle cx="12" cy="8" r="3.5"/><path d="M5 20c1-4 4-6 7-6s6 2 7 6"/>',
  gruppe: '<circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.5"/><path d="M3 19c.8-3.4 3.2-5 6-5s5.2 1.6 6 5M15 14.2c2.6-.3 4.8 1.1 5.6 4.3"/>',
  hantel: '<path d="M6.5 6.5v11M17.5 6.5v11M3 9v6M21 9v6M6.5 12h11"/>',
  preis: '<path d="M12 3v18M16.5 7.5c-.8-1.5-2.5-2.3-4.5-2.3-2.6 0-4.3 1.3-4.3 3.1 0 4.4 9 2.5 9 6.9 0 1.9-1.9 3.3-4.7 3.3-2.2 0-4-.9-4.8-2.5"/>',
};

function preisText(p) {
  return p > 0 ? `$${p} pro Person` : "kostenlos";
}

// Listeneintrag: oben Symbol, Titel und Status, darunter Infos über die volle Breite, ganz unten die Knöpfe.
function eintrag({ symbol, titel, unter, marke, markeAn, infos = [], knoepfe = [] }) {
  const e = el("div", "hx-eintrag");
  const kopf = el("div", "hx-eintrag-kopf");
  const t = el("div", "hx-eintrag-titel");
  t.append(text("b", "", titel));
  if (unter) t.append(text("small", "", unter));
  kopf.append(symbol, t);
  if (marke) kopf.append(text("span", "hx-marke" + (markeAn ? " is-an" : ""), marke));
  e.append(kopf);
  const zeilen = infos.filter((i) => i && i[1]);
  if (zeilen.length) {
    const box = el("div", "hx-eintrag-info");
    for (const [icon, inhalt] of zeilen) {
      const z = el("div", "hx-info-zeile", svg(ICON[icon] || icon));
      z.append(text("span", "", inhalt));
      box.append(z);
    }
    e.append(box);
  }
  if (knoepfe.length) {
    const k = el("div", "hx-eintrag-knoepfe");
    k.append(...knoepfe);
    e.append(k);
  }
  return e;
}

function knopf(titel, voll, aktion) {
  const k = text("button", "hx-knopf" + (voll ? " is-voll" : ""), titel);
  k.type = "button";
  k.onclick = () => aktion(k);
  return k;
}

const geld = (n) => "$" + Number(n || 0).toLocaleString("de-DE");

function datumKopf() {
  const d = $("datum");
  if (d) d.textContent = new Date().toLocaleDateString("de-DE", { weekday: "long", day: "numeric", month: "long" });
}

// Hell oder dunkel: lb-phone liefert die Einstellung über getSettings/onSettingsChange (display.theme),
// andere Handys über ?theme=dark in der Adresse, sonst die Einstellung des Systems.
const thema = new URLSearchParams(location.search).get("theme");

function themaSetzen(wert) {
  if (wert === "dark" || wert === "light") document.documentElement.dataset.theme = wert;
}

function themaVomHandy() {
  try {
    const da = globalThis.settings;
    if (da && da.display) themaSetzen(da.display.theme);
    if (typeof getSettings === "function")
      getSettings().then((e) => themaSetzen(e && e.display && e.display.theme)).catch(() => {});
    if (typeof onSettingsChange === "function" && !themaVomHandy.angemeldet) {
      themaVomHandy.angemeldet = true;
      onSettingsChange((e) => themaSetzen(e && e.display && e.display.theme));
    }
  } catch {}
}

if (thema) themaSetzen(thema);
else if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) themaSetzen("dark");
else themaSetzen("light");
themaVomHandy();
window.addEventListener("message", (e) => {
  if (e.data === "componentsLoaded") themaVomHandy();
});

function groesse() {
  document.documentElement.style.zoom = Math.min(1, window.innerWidth / 390);
}
window.addEventListener("resize", groesse);
groesse();
datumKopf();
