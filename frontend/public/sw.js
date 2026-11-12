// CMS Portal service worker. Caches the app shell; network-first for API, cache-first for static.
// Bump CACHE_NAME to force clients to refresh their cached assets.
const CACHE_NAME = "cms-portal-v2";
const SHELL = ["/", "/portal", "/manifest.webmanifest", "/icon-192.svg", "/icon-512.svg"];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((c) => c.addAll(SHELL)).catch(() => {}));
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil((async () => {
    const names = await caches.keys();
    await Promise.all(names.filter((n) => n !== CACHE_NAME).map((n) => caches.delete(n)));
    await self.clients.claim();
  })());
});

self.addEventListener("fetch", (event) => {
  const req = event.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // API: network-first, no caching (auth sensitive).
  if (url.pathname.startsWith("/api/")) {
    event.respondWith(fetch(req).catch(() => new Response("", { status: 503 })));
    return;
  }

  // Static: cache-first, update in background.
  event.respondWith((async () => {
    const cache = await caches.open(CACHE_NAME);
    const cached = await cache.match(req);
    const fetching = fetch(req).then((res) => {
      if (res.ok && res.type === "basic") cache.put(req, res.clone()).catch(() => {});
      return res;
    }).catch(() => cached);
    return cached || fetching;
  })());
});
