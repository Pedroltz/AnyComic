const CACHE_NAME = 'anycomic-v2';

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', event => event.waitUntil((async () => {
    // Remove caches from previous versions
    const keys = await caches.keys();
    await Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)));
    await self.clients.claim();
})()));

// A fetch handler is required for the browser to treat the site as installable
// (without it Chrome only offers "Create shortcut", not "Install").
// Strategy: cache-first for same-origin static assets (css/js/images/fonts);
// page navigations and everything else go straight to the network so dynamic
// and auth-dependent content is never served stale.
self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    const isStaticAsset = /\.(css|js|png|jpe?g|webp|gif|svg|ico|woff2?|ttf)$/i.test(url.pathname);
    if (!isStaticAsset) return;

    event.respondWith((async () => {
        const cache = await caches.open(CACHE_NAME);
        const cached = await cache.match(req);
        if (cached) return cached;
        try {
            const res = await fetch(req);
            if (res && res.status === 200) cache.put(req, res.clone());
            return res;
        } catch {
            return cached || Response.error();
        }
    })());
});
