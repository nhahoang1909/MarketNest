/**
 * Alpine.js product form component — used in seller product create/edit pages
 */
document.addEventListener("alpine:init", () => {
    Alpine.data("productForm", () => ({
        name: "",
        description: "",
        price: 0,
        stock: 0,
        images: [],
        variants: [],
        isSubmitting: false,

        addVariant() {
            this.variants.push({
                name: "",
                sku: "",
                price: 0,
                stock: 0,
            });
        },

        removeVariant(index) {
            this.variants.splice(index, 1);
        },

        async submit() {
            this.isSubmitting = true;
            // Submission logic will be wired via HTMX
        },

        reset() {
            this.name = "";
            this.description = "";
            this.price = 0;
            this.stock = 0;
            this.images = [];
            this.variants = [];
            this.isSubmitting = false;
        },
    }));
});

