/**
 * HTMX event listeners and helpers
 * - Listens for custom HX-Trigger events from server responses
 * - Injects anti-forgery tokens into HTMX requests
 */
import {ErrorMessages, HtmxConfig, HttpStatus, Routes, ToastConfig} from "../constants.js";

(function () {
    "use strict";

    // ── Anti-forgery token injection ──────────────────────────────
    document.body.addEventListener("htmx:configRequest", (event) => {
        const tokenEl =
            document.querySelector(HtmxConfig.CSRF_INPUT_SELECTOR) ??
            document.querySelector(HtmxConfig.CSRF_META_SELECTOR);
        if (tokenEl) {
            const token = tokenEl.value ?? tokenEl.content;
            event.detail.headers[HtmxConfig.CSRF_HEADER] = token;
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
        const {message, type, duration} = event.detail ?? {};
        if (window.Alpine && message) {
            Alpine.store("toasts").add(
                message,
                type ?? ToastConfig.DEFAULT_TYPE,
                duration ?? ToastConfig.DEFAULT_DURATION_MS,
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
        if (status === HttpStatus.UNAUTHORIZED) {
            window.location.href = Routes.AUTH_LOGIN;
        } else if (status >= HttpStatus.INTERNAL_SERVER_ERROR && window.Alpine) {
            Alpine.store("toasts").error(ErrorMessages.GENERIC);
        }
    });

    // ── HTMX request indicator class on body ──────────────────────
    document.body.addEventListener("htmx:beforeRequest", () => {
        document.body.classList.add(HtmxConfig.REQUEST_CLASS);
    });
    document.body.addEventListener("htmx:afterRequest", () => {
        document.body.classList.remove(HtmxConfig.REQUEST_CLASS);
    });
})();
