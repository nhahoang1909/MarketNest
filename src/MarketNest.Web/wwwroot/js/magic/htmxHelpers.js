/**
 * HTMX event listeners and helpers
 * - Listens for custom HX-Trigger events from server responses
 * - Injects anti-forgery tokens into HTMX requests
 */
(function () {
    "use strict";

    // ── Anti-forgery token injection ──────────────────────────────
    document.body.addEventListener("htmx:configRequest", (event) => {
        const tokenEl =
            document.querySelector(
                'input[name="__RequestVerificationToken"]',
            ) ?? document.querySelector('meta[name="csrf-token"]');
        if (tokenEl) {
            const token = tokenEl.value ?? tokenEl.content;
            event.detail.headers["RequestVerificationToken"] = token;
        }
    });

    // ── Cart updated ──────────────────────────────────────────────
    document.body.addEventListener("cartUpdated", (event) => {
        const data = event.detail ?? {};
        if (window.Alpine) {
            Alpine.store("cart").updateFromServer(data);
        }
    });

    // ── Toast show ────────────────────────────────────────────────
    document.body.addEventListener("toastShow", (event) => {
        const { message, type, duration } = event.detail ?? {};
        if (window.Alpine && message) {
            Alpine.store("toasts").add(
                message,
                type ?? "info",
                duration ?? 5000,
            );
        }
    });

    // ── Modal close ───────────────────────────────────────────────
    document.body.addEventListener("modalClose", () => {
        window.dispatchEvent(new CustomEvent("close-modal"));
    });

    // ── Drawer close ──────────────────────────────────────────────
    document.body.addEventListener("drawerClose", () => {
        window.dispatchEvent(new CustomEvent("close-drawer"));
    });

    // ── Global HTMX error handling ────────────────────────────────
    document.body.addEventListener("htmx:responseError", (event) => {
        const status = event.detail.xhr?.status;
        if (status === 401) {
            window.location.href = "/account/login";
        } else if (status >= 500 && window.Alpine) {
            Alpine.store("toasts").error(
                "Something went wrong. Please try again.",
            );
        }
    });

    // ── HTMX request indicator class on body ──────────────────────
    document.body.addEventListener("htmx:beforeRequest", () => {
        document.body.classList.add("htmx-request");
    });
    document.body.addEventListener("htmx:afterRequest", () => {
        document.body.classList.remove("htmx-request");
    });
})();
