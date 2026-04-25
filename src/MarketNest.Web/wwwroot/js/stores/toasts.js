/**
 * Alpine.js toast notification store
 */
import { ToastConfig } from "../constants.js";

document.addEventListener("alpine:init", () => {
    Alpine.store("toasts", {
        items: [],
        _nextId: 1,

        add(message, type = ToastConfig.DEFAULT_TYPE, duration = ToastConfig.DEFAULT_DURATION_MS) {
            const id = this._nextId++;
            this.items.push({ id, message, type, visible: true });

            if (duration > 0) {
                setTimeout(() => this.remove(id), duration);
            }

            return id;
        },

        remove(id) {
            const idx = this.items.findIndex((t) => t.id === id);
            if (idx !== -1) {
                this.items[idx].visible = false;
                // Remove from DOM after transition
                setTimeout(() => {
                    this.items = this.items.filter((t) => t.id !== id);
                }, ToastConfig.DOM_REMOVAL_DELAY_MS);
            }
        },

        success(message, duration) {
            return this.add(message, "success", duration);
        },
        error(message, duration) {
            return this.add(message, "error", duration);
        },
        warning(message, duration) {
            return this.add(message, "warning", duration);
        },
        info(message, duration) {
            return this.add(message, "info", duration);
        },

        /* Aliases used by layouts */
        dismiss(id) {
            return this.remove(id);
        },
        show(message, type, duration) {
            return this.add(message, type, duration);
        },
    });
});
