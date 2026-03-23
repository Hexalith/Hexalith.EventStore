// Hexalith Admin UI — JS Interop
// Keyboard shortcuts (Ctrl+K, Ctrl+B) and localStorage helpers

window.hexalithAdmin = {
    _shortcutHandlers: new Map(),

    // Register global keyboard shortcut handler
    registerShortcuts: function (dotNetRef) {
        const registrationId = `shortcuts-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        const handler = function (e) {
            // Ctrl+K: Command palette
            if (e.ctrlKey && e.key === "k") {
                e.preventDefault();
                dotNetRef.invokeMethodAsync("OnCommandPaletteShortcut");
            }
            // Ctrl+B: Toggle sidebar
            if (e.ctrlKey && e.key === "b") {
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

    focusCommandPaletteSearch: function () {
        const searchElement = document.querySelector(
            ".command-palette-search input, .command-palette-search",
        );
        if (searchElement && typeof searchElement.focus === "function") {
            searchElement.focus();
        }
    },
};

