/**
 * Star rating Alpine.js component
 * Usage: x-data="starRating({ value: 0 })"
 */
document.addEventListener("alpine:init", () => {
    Alpine.data("starRating", ({ value = 0 } = {}) => ({
        value: value,
        hover: 0,

        setHover(star) {
            this.hover = star;
        },

        clearHover() {
            this.hover = 0;
        },

        select(star) {
            this.value = this.value === star ? 0 : star;
        },
    }));
});
