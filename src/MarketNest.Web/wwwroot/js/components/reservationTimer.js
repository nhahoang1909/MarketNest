/**
 * Reservation countdown timer Alpine.js component
 * Usage: x-data="reservationTimer({ expiresAt: '2026-04-25T12:00:00Z' })"
 */
import { TimerConfig } from "../constants.js";

document.addEventListener("alpine:init", () => {
    Alpine.data("reservationTimer", ({expiresAt = ""} = {}) => ({
        expiresAt,
        remaining: "",
        expired: false,
        _interval: null,

        init() {
            this._tick();
            this._interval = setInterval(() => this._tick(), TimerConfig.TICK_INTERVAL_MS);
        },

        destroy() {
            if (this._interval) clearInterval(this._interval);
        },

        _tick() {
            if (!this.expiresAt) {
                this.remaining = TimerConfig.PLACEHOLDER;
                return;
            }

            const now = Date.now();
            const end = new Date(this.expiresAt).getTime();
            const diff = end - now;

            if (diff <= 0) {
                this.remaining = TimerConfig.EXPIRED;
                this.expired = true;
                clearInterval(this._interval);
                this.$dispatch("reservation-expired");
                return;
            }

            const minutes = Math.floor(diff / TimerConfig.MS_PER_MINUTE);
            const seconds = Math.floor((diff % TimerConfig.MS_PER_MINUTE) / TimerConfig.MS_PER_SECOND);
            this.remaining = `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
        },
    }));
});
