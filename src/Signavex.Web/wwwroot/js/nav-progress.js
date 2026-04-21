// Thin progress bar at the top of the viewport that signals in-flight
// navigation during Blazor enhanced nav transitions and form submissions.
// Pattern: start on any same-origin <a> click or <form> submit, finish on
// Blazor's `enhancedload` event. A safety timeout hides the bar if the
// event never fires (e.g. a non-enhanced full page load).
//
// Blazor enhanced nav morphs <body>'s DOM tree on navigation, which would
// detach a bar element appended to body. We attach to documentElement and
// check `isConnected` on every start() so the bar is recreated if morphing
// ever detaches it.
(function () {
    'use strict';

    let bar;
    let safetyTimeoutId;
    let startTimestamp;

    function createBar() {
        const el = document.createElement('div');
        el.id = 'nav-progress';
        el.setAttribute('data-permanent', '');
        el.style.cssText = [
            'position:fixed',
            'top:0',
            'left:0',
            'height:3px',
            'width:0',
            'z-index:99999',
            'background:var(--color-primary, #4f46e5)',
            'box-shadow:0 0 10px 0 var(--color-primary, #4f46e5)',
            'opacity:0',
            'transition:width 0.25s ease-out, opacity 0.3s ease-out',
            'pointer-events:none'
        ].join(';');
        document.documentElement.appendChild(el);
        return el;
    }

    function ensureBar() {
        if (bar && bar.isConnected) return bar;
        bar = createBar();
        return bar;
    }

    function start() {
        const b = ensureBar();
        startTimestamp = performance.now();
        // Reset for repeat starts
        b.style.transition = 'none';
        b.style.width = '0';
        b.style.opacity = '1';
        // Force reflow so the 0 → 80% transition actually animates
        void b.offsetWidth;
        b.style.transition = 'width 0.8s cubic-bezier(0.1, 0.9, 0.3, 1), opacity 0.3s ease-out';
        b.style.width = '80%';

        clearTimeout(safetyTimeoutId);
        // If enhancedload never fires (rare — full page load, or error),
        // hide the bar after 10s so it doesn't sit there forever.
        safetyTimeoutId = setTimeout(doFinish, 10000);
    }

    function doFinish() {
        const b = ensureBar();
        b.style.transition = 'width 0.2s ease-out, opacity 0.3s ease-out 0.15s';
        b.style.width = '100%';
        setTimeout(() => {
            // Re-check connectedness — bar may have been recreated between calls.
            const b2 = ensureBar();
            b2.style.opacity = '0';
            setTimeout(() => {
                const b3 = ensureBar();
                b3.style.width = '0';
            }, 350);
        }, 150);
        clearTimeout(safetyTimeoutId);
    }

    function done() {
        // Enforce a minimum visible duration so fast (cached) navigations
        // still flash the bar clearly instead of flickering invisibly.
        const elapsed = performance.now() - (startTimestamp || 0);
        const minVisible = 350;
        if (elapsed < minVisible) {
            setTimeout(doFinish, minVisible - elapsed);
        } else {
            doFinish();
        }
    }

    // Fire on clicks of same-origin anchors that will trigger enhanced nav.
    // Skip modifier-keyed clicks (open in new tab), non-primary buttons,
    // target="_blank", cross-origin links, and explicit opt-outs.
    document.addEventListener('click', function (e) {
        if (e.button !== 0) return;
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        if (e.defaultPrevented) return;
        if (!(e.target instanceof Element)) return;

        const link = e.target.closest('a');
        if (!link) return;
        if (link.target && link.target !== '' && link.target !== '_self') return;

        const href = link.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;

        // Respect an opt-out for links that won't actually navigate enhanced-ly.
        if (link.getAttribute('data-enhance-nav') === 'false') return;

        // Skip cross-origin — browser will do a full page load.
        try {
            const url = new URL(href, window.location.href);
            if (url.origin !== window.location.origin) return;
        } catch (_) {
            return;
        }

        start();
    }, true);

    // Fire on form submissions — the admin POST buttons use enhanced forms.
    document.addEventListener('submit', function (e) {
        if (e.defaultPrevented) return;
        const form = e.target;
        if (!form || form.tagName !== 'FORM') return;
        if (form.getAttribute('data-enhance-nav') === 'false') return;
        start();
    }, true);

    // Hide when Blazor finishes enhanced navigation. Blazor.addEventListener
    // may not be ready when this script runs — retry until it is.
    function registerBlazorHook() {
        if (typeof Blazor === 'undefined' || typeof Blazor.addEventListener !== 'function') {
            setTimeout(registerBlazorHook, 50);
            return;
        }
        Blazor.addEventListener('enhancedload', done);
    }
    registerBlazorHook();
})();
