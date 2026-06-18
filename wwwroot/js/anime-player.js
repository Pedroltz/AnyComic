'use strict';

// Exposed globally so the inline onclick="toggleDesc()" in the view can call it
function toggleDesc() {
    var desc = document.getElementById('adDesc');
    var btn  = document.getElementById('adReadMore');
    var icon = document.getElementById('adReadMoreIcon');
    if (!desc) return;
    var expanded = desc.classList.toggle('expanded');
    var label = btn && btn.querySelector('.ad-readmore-label');
    if (label) label.textContent = expanded ? 'Read less' : 'Read more';
    if (icon)  icon.style.transform = expanded ? 'rotate(180deg)' : '';
}

// Exposed globally for the inline onclick="toggleEpisodes()" in the view.
// Collapses/expands the overflow portion of the episode list.
function toggleEpisodes() {
    var list = document.getElementById('adEpList');
    var btn  = document.getElementById('adEpToggle');
    if (!list || !btn) return;
    var collapsed = list.classList.toggle('ad-ep-collapsed');
    btn.classList.toggle('expanded', !collapsed);
    var label = btn.querySelector('.ad-ep-toggle-label');
    if (label) label.textContent = collapsed ? (btn.getAttribute('data-more-label') || 'Show more') : 'Show less';
    if (collapsed) list.scrollTop = 0;
}

document.addEventListener('DOMContentLoaded', function () {
    // ── Plyr initialization ──────────────────────────────────────────────────
    var playerEl = document.getElementById('ad-player');
    if (playerEl && window.Plyr) {
        new Plyr(playerEl, {
            ratio:    '16:9',
            autoplay: false,
            keyboard: { focused: true, global: false },
            controls: [
                'play-large', 'rewind', 'play', 'fast-forward', 'progress',
                'current-time', 'duration', 'mute', 'volume', 'captions',
                'settings', 'pip', 'airplay', 'fullscreen'
            ],
            settings: ['captions', 'quality', 'speed'],
            speed:    { selected: 1, options: [0.5, 0.75, 1, 1.25, 1.5, 2] },
            youtube:  { noCookie: true, rel: 0, modestbranding: 1 },
            vimeo:    { byline: false, portrait: false, title: false }
        });
    }

    // ── Scroll active episode into view inside the sidebar ───────────────────
    var activeCard = document.getElementById('activeEpCard');
    if (activeCard) {
        // If the active episode lives in the collapsed (hidden) portion,
        // expand the list first so "Now playing" is actually visible.
        if (activeCard.classList.contains('ad-ep-extra')) {
            var epList = document.getElementById('adEpList');
            if (epList && epList.classList.contains('ad-ep-collapsed')) {
                toggleEpisodes();
            }
        }
        var list = activeCard.closest('.ad-ep-list');
        if (list) activeCard.scrollIntoView({ block: 'nearest' });
    }
});
