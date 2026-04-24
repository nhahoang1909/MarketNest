/**
 * Alpine.js toast notification store
 */
document.addEventListener("alpine:init", () => {
    Alpine.store("toasts", {
        items: [],
        _nextId: 1,

        add(message, type = "info", duration = 5000) {
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
                }, 300);
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
    });
});
