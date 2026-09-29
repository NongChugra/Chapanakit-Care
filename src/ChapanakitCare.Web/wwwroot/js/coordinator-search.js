(() => {
    const searchForm = document.querySelector('.coordinator-search');
    if (!searchForm) return;

    const storageKey = 'coordinator-search-scroll';
    try {
        const saved = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
        sessionStorage.removeItem(storageKey);
        if (saved && saved.url === location.pathname + location.search &&
            Date.now() - saved.savedAt < 60000 && Number.isFinite(saved.x) && Number.isFinite(saved.y)) {
            const previousRestoration = history.scrollRestoration;
            history.scrollRestoration = 'manual';
            window.addEventListener('pageshow', () => {
                requestAnimationFrame(() => {
                    window.scrollTo({ left: saved.x, top: saved.y, behavior: 'instant' });
                    history.scrollRestoration = previousRestoration;
                });
            }, { once: true });
        }
    } catch {
        // Search still works normally when browser storage is unavailable.
    }

    searchForm.addEventListener('submit', () => {
        const target = new URL(searchForm.action, location.href);
        target.search = new URLSearchParams(new FormData(searchForm)).toString();
        try {
            sessionStorage.setItem(storageKey, JSON.stringify({
                url: target.pathname + target.search,
                x: window.scrollX,
                y: window.scrollY,
                savedAt: Date.now()
            }));
        } catch {
            // Do not prevent the native GET submission if storage is blocked.
        }
    });
})();
