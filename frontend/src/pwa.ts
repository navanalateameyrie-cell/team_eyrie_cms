// PWA bootstrap: registers the service worker on first load and routes the
// `beforeinstallprompt` event through a custom DOM event so any component can
// decide when to surface the install affordance.

export interface BeforeInstallPromptEvent extends Event {
  readonly platforms: string[];
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed"; platform: string }>;
}

let deferredPrompt: BeforeInstallPromptEvent | null = null;

export function getDeferredInstallPrompt(): BeforeInstallPromptEvent | null {
  return deferredPrompt;
}

export function registerPwa() {
  if (typeof window === "undefined") return;

  // Only register in production — Vite dev server's HMR + SW caching misbehave together.
  if ("serviceWorker" in navigator && import.meta.env.PROD) {
    window.addEventListener("load", () => {
      navigator.serviceWorker.register("/sw.js").catch((e) => console.warn("SW register failed", e));
    });
  }

  window.addEventListener("beforeinstallprompt", (e) => {
    e.preventDefault();
    deferredPrompt = e as BeforeInstallPromptEvent;
    window.dispatchEvent(new CustomEvent("cms:installprompt-available"));
  });

  window.addEventListener("appinstalled", () => {
    deferredPrompt = null;
    window.dispatchEvent(new CustomEvent("cms:installprompt-consumed"));
  });
}

/** Fires the stored prompt and clears it. Returns the user's outcome. */
export async function triggerInstallPrompt(): Promise<"accepted" | "dismissed" | "unavailable"> {
  if (!deferredPrompt) return "unavailable";
  const p = deferredPrompt;
  deferredPrompt = null;
  await p.prompt();
  const choice = await p.userChoice;
  window.dispatchEvent(new CustomEvent("cms:installprompt-consumed"));
  return choice.outcome;
}
