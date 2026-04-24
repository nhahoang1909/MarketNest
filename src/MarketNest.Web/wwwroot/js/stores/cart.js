/**
 * Alpine.js cart store
 * Tracks cart count, total, reservations and nearest expiry.
 */
document.addEventListener("alpine:init", () => {
    Alpine.store("cart", {
        count: 0,
        total: 0,
        reservations: [],
        nearestExpiry: null,

        init() {
            // Hydrate from server-rendered data attribute if available
            const el = document.querySelector("[data-cart-count]");
            if (el) {
                this.count = parseInt(el.dataset.cartCount, 10) || 0;
                this.total = parseFloat(el.dataset.cartTotal) || 0;
            }
        },

        updateFromServer(data) {
            this.count = data.count ?? this.count;
            this.total = data.total ?? this.total;
            this.reservations = data.reservations ?? this.reservations;
            this.nearestExpiry = data.nearestExpiry ?? this.nearestExpiry;
        },

        clear() {
            this.count = 0;
            this.total = 0;
            this.reservations = [];
            this.nearestExpiry = null;
        },
    });
});
