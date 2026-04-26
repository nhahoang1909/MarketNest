/**
 * MarketNest — Alpine.js bootstrap
 *
 * Loads all stores, components, and magic helpers, then starts Alpine.
 * Script order in _Layout.cshtml should be:
 *   1. alpine.js (defer)
 *   2. htmx.js
 *   3. This file (defer)
 */

import {CurrencyDefaults, DisplayStrings, TimeIntervals, TimerConfig} from "./constants.js";

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
    Alpine.magic("currency", () => (amount, currency = CurrencyDefaults.CURRENCY) => {
        return new Intl.NumberFormat(CurrencyDefaults.LOCALE, {
            style: "currency",
            currency,
            minimumFractionDigits: CurrencyDefaults.MIN_FRACTION_DIGITS,
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
            (Date.now() - new Date(isoString).getTime()) / TimerConfig.MS_PER_SECOND,
        );

        const intervals = [
            {label: "year", seconds: TimeIntervals.YEAR},
            {label: "month", seconds: TimeIntervals.MONTH},
            {label: "week", seconds: TimeIntervals.WEEK},
            {label: "day", seconds: TimeIntervals.DAY},
            {label: "hour", seconds: TimeIntervals.HOUR},
            {label: "minute", seconds: TimeIntervals.MINUTE},
        ];

        for (const {label, seconds: s} of intervals) {
            const count = Math.floor(seconds / s);
            if (count >= 1) {
                return `${count} ${label}${count !== 1 ? "s" : ""} ago`;
            }
        }
        return DisplayStrings.JUST_NOW;
    });
});
