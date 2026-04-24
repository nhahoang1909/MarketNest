/**
 * MarketNest — Alpine.js bootstrap
 *
 * Loads all stores, components, and magic helpers, then starts Alpine.
 * Script order in _Layout.cshtml should be:
 *   1. alpine.js (defer)
 *   2. htmx.js
 *   3. This file (defer)
 */

// ── Stores ────────────────────────────────────────────────────────
import "./stores/cart.js";
import "./stores/toasts.js";
import "./stores/user.js";

// ── Components ────────────────────────────────────────────────────
import "./components/starRating.js";
import "./components/imageUploader.js";
import "./components/confirmDialog.js";
import "./components/reservationTimer.js";
import "./components/infiniteScroll.js";
import "./components/datePicker.js";
import "./components/multiSelect.js";

// ── HTMX helpers (non-Alpine, self-executing) ─────────────────────
import "./magic/htmxHelpers.js";

// ── Magic helpers ─────────────────────────────────────────────────
document.addEventListener("alpine:init", () => {
    /**
     * $currency(amount, currency) — format a number as currency
     * Usage: <span x-text="$currency(29.99)"></span>
     */
    Alpine.magic("currency", () => (amount, currency = "USD") => {
        return new Intl.NumberFormat("en-US", {
            style: "currency",
            currency,
            minimumFractionDigits: 2,
        }).format(amount ?? 0);
    });

    /**
     * $date(isoString) — format an ISO date string
     * Usage: <span x-text="$date('2026-04-25')"></span>
     */
    Alpine.magic("date", () => (isoString, options) => {
        if (!isoString) return "";
        const defaults = {year: "numeric", month: "short", day: "numeric"};
        return new Date(isoString).toLocaleDateString(
            undefined,
            options ?? defaults,
        );
    });

    /**
     * $timeAgo(isoString) — relative time (e.g. "3 hours ago")
     * Usage: <span x-text="$timeAgo(review.createdAt)"></span>
     */
    Alpine.magic("timeAgo", () => (isoString) => {
        if (!isoString) return "";
        const seconds = Math.floor(
            (Date.now() - new Date(isoString).getTime()) / 1000,
        );

        const intervals = [
            {label: "year", seconds: 31536000},
            {label: "month", seconds: 2592000},
            {label: "week", seconds: 604800},
            {label: "day", seconds: 86400},
            {label: "hour", seconds: 3600},
            {label: "minute", seconds: 60},
        ];

        for (const {label, seconds: s} of intervals) {
            const count = Math.floor(seconds / s);
            if (count >= 1) {
                return `${count} ${label}${count !== 1 ? "s" : ""} ago`;
            }
        }
        return "just now";
    });
});
