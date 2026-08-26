// Hexalith Admin UI — JS Interop
// Keyboard shortcuts (Ctrl+K, Ctrl+B) and localStorage helpers

window.hexalithAdmin = {
    _shortcutHandlers: new Map(),

    // Register global keyboard shortcut handler
    registerShortcuts: function (dotNetRef) {
        const registrationId = `shortcuts-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        const handler = function (e) {
            // Only Ctrl+letter — exclude Shift/Alt/Meta so browser shortcuts
            // (Ctrl+Shift+B bookmarks bar, Ctrl+Shift+K Firefox web console,
            // AltGr-driven characters which raise ctrlKey+altKey) fall through.
            if (!e.ctrlKey || e.shiftKey || e.altKey || e.metaKey) {
                return;
            }
            const key = e.key?.toLowerCase();
            // Ctrl+K: Command palette
            if (key === "k") {
                e.preventDefault();
                dotNetRef.invokeMethodAsync("OnCommandPaletteShortcut");
                return;
            }
            // Ctrl+B: Toggle sidebar
            if (key === "b") {
                e.preventDefault();
                dotNetRef.invokeMethodAsync("OnToggleSidebarShortcut");
            }
        };

        this._shortcutHandlers.set(registrationId, handler);
        document.addEventListener("keydown", handler);
        return registrationId;
    },

    unregisterShortcuts: function (registrationId) {
        const handler = this._shortcutHandlers.get(registrationId);
        if (!handler) {
            return;
        }

        document.removeEventListener("keydown", handler);
        this._shortcutHandlers.delete(registrationId);
    },

    // localStorage helpers with try/catch for private browsing
    getLocalStorage: function (key) {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    },

    setLocalStorage: function (key, value) {
        try {
            localStorage.setItem(key, value);
        } catch {
            // Quota exceeded or private browsing — silently fail
        }
    },

    // Get viewport width for responsive sidebar state
    getViewportWidth: function () {
        return window.innerWidth;
    },

    getScrollTop: function () {
        return window.scrollY || document.documentElement.scrollTop || 0;
    },

    setScrollTop: function (top) {
        window.scrollTo(0, typeof top === "number" ? top : 0);
    },

    _viewportListeners: new Map(),
    _viewportMediaQueries: new Map(),
    _viewportChangeHandlers: new Map(),
    _viewportIdCounter: 0,

    registerViewportListener: function (dotNetRef, breakpoint) {
        const listenerId = `vp-${++this._viewportIdCounter}`;
        const mediaQuery = window.matchMedia(`(min-width: ${breakpoint}px)`);
        const handler = (e) => {
            const ref = this._viewportListeners.get(listenerId);
            if (ref) {
                ref.invokeMethodAsync("OnViewportWidthChanged", e.matches);
            }
        };
        mediaQuery.addEventListener("change", handler);
        this._viewportListeners.set(listenerId, dotNetRef);
        this._viewportMediaQueries.set(listenerId, mediaQuery);
        this._viewportChangeHandlers.set(listenerId, handler);
        return listenerId;
    },

    unregisterViewportListener: function (listenerId) {
        const mediaQuery = this._viewportMediaQueries.get(listenerId);
        const handler = this._viewportChangeHandlers.get(listenerId);
        if (mediaQuery && handler) {
            mediaQuery.removeEventListener("change", handler);
        }
        this._viewportListeners.delete(listenerId);
        this._viewportMediaQueries.delete(listenerId);
        this._viewportChangeHandlers.delete(listenerId);
    },

    copyToClipboard: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            return false;
        }
    },

    focusCommandPaletteSearch: function () {
        const searchElement = document.querySelector(
            ".command-palette-search input, .command-palette-search",
        );
        if (searchElement && typeof searchElement.focus === "function") {
            searchElement.focus();
        }
    },

    // Theme color-scheme helpers for v5 migration
    // Sets data-theme attribute (triggers project CSS custom property recalc via [data-theme] selectors)
    // AND color-scheme property (triggers FluentUI v5 web component repaint).
    // Attribute set first so project tokens are ready before FluentUI repaints.
    setColorScheme: function (scheme) {
        document.documentElement.setAttribute('data-theme', scheme);
        document.documentElement.style.setProperty('color-scheme', scheme);
    },

    removeColorScheme: function () {
        document.documentElement.removeAttribute('data-theme');
        document.documentElement.style.removeProperty('color-scheme');
    },

    // Accessibility: keyboard activation/navigation default-action suppression.
    // The activation/navigation LOGIC lives in the Blazor @onkeydown handlers; this single
    // delegated listener only cancels the browser default that would otherwise fire alongside it:
    //   - Space (or legacy "Spacebar") on a custom role="button" element ([data-activate-button])
    //     scrolls the page one viewport — suppress it. A blanket Razor @onkeydown:preventDefault
    //     cannot be used because it would also cancel Tab (trapping focus); gating by key here is
    //     the correct, focus-safe equivalent.
    //   - Arrow / Home / End inside a roving-tabindex grid ([data-roving-grid]) scroll the page —
    //     suppress them so arrow-key cell navigation does not also move the viewport. Tab is left
    //     untouched so focus can always leave the grid.
    _a11yKeydownRegistered: false,

    registerA11yKeyboard: function () {
        if (this._a11yKeydownRegistered) {
            return;
        }
        this._a11yKeydownRegistered = true;
        document.addEventListener("keydown", function (e) {
            const target = e.target;
            if (!target || typeof target.closest !== "function") {
                return;
            }
            if ((e.key === " " || e.key === "Spacebar") && target.closest("[data-activate-button]")) {
                e.preventDefault();
                return;
            }
            if ((e.key === "ArrowUp" || e.key === "ArrowDown" || e.key === "ArrowLeft"
                    || e.key === "ArrowRight" || e.key === "Home" || e.key === "End")
                && target.closest("[data-roving-grid]")) {
                e.preventDefault();
            }
        });
    },

    // Move DOM focus to the roving-tabindex grid cell at (row, col), addressed by data attributes.
    // No-op when the grid or cell is absent (e.g. under bUnit's loose JS interop, where this is mocked).
    focusGridCell: function (gridSelector, row, col) {
        const grid = document.querySelector(gridSelector);
        if (!grid) {
            return;
        }
        const cell = grid.querySelector(`[data-row="${row}"][data-col="${col}"]`);
        if (cell && typeof cell.focus === "function") {
            cell.focus();
        }
    },
};

// Register the accessibility keydown listener once at script load. The listener is delegated on
// document and uses event.target.closest(...), so it covers elements added later by Blazor renders.
window.hexalithAdmin.registerA11yKeyboard();
