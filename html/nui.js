// Verbindung zwischen NUI-Oberfläche und Ressource. Im normalen Browser (ohne FiveM) läuft alles im Vorschau-Modus.
const Nui = (() => {
  const inGame = typeof GetParentResourceName === "function";
  const resource = inGame ? GetParentResourceName() : "vorschau";
  const handlers = {};

  window.addEventListener("message", (e) => {
    const data = e.data || {};
    const fn = handlers[data.action];
    if (fn) fn(data);
  });

  window.addEventListener("keydown", (e) => {
    if (e.key === "Escape") Nui.close();
  });

  return {
    inGame,

    on(action, fn) {
      handlers[action] = fn;
    },

    async post(name, data = {}) {
      if (!inGame) {
        console.log(`[nui] ${name}`, data);
        return null;
      }
      try {
        const r = await fetch(`https://${resource}/${name}`, {
          method: "POST",
          headers: { "Content-Type": "application/json; charset=UTF-8" },
          body: JSON.stringify(data),
        });
        return await r.json().catch(() => null);
      } catch {
        return null;
      }
    },

    show(el) {
      el.hidden = false;
    },

    hide(el) {
      el.hidden = true;
    },

    close() {
      document.querySelectorAll("[data-nui-root]").forEach((el) => (el.hidden = true));
      Nui.post("close");
    },

    toast(title, text = "", type = "", ms = 4000) {
      let box = document.querySelector(".tu-toasts");
      if (!box) {
        box = document.createElement("div");
        box.className = "tu-toasts";
        document.body.append(box);
      }
      const t = document.createElement("div");
      t.className = `tu-toast${type ? " is-" + type : ""}`;
      const wrap = document.createElement("div");
      const b = document.createElement("b");
      b.textContent = title;
      const s = document.createElement("span");
      s.textContent = text;
      wrap.append(b, s);
      t.append(wrap);
      box.append(t);
      setTimeout(() => {
        t.classList.add("is-leaving");
        setTimeout(() => t.remove(), 260);
      }, ms);
    },
  };
})();
