const CACHE_NAME = 'anycomic-v1';

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(clients.claim()));

// TODO: add caching strategy when implementing full PWA
// e.g. cache-first for static assets, network-first for API/manga pages
