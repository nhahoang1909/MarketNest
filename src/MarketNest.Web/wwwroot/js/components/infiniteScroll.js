/**
 * Infinite scroll Alpine.js component using IntersectionObserver
 * Usage: x-data="infiniteScroll({ url: '/api/products', targetId: '#product-list' })"
 */
document.addEventListener("alpine:init", () => {
    Alpine.data("infiniteScroll", ({url = "", targetId = ""} = {}) => ({
        page: 1,
        loading: false,
        hasMore: true,
        url,
        targetId,
        _observer: null,

        init() {
            this.$nextTick(() => {
                const sentinel =
                    this.$refs.sentinel ??
                    this.$el.querySelector("[data-sentinel]");
                if (!sentinel) return;

                this._observer = new IntersectionObserver(
                    (entries) => {
                        if (
                            entries[0].isIntersecting &&
                            !this.loading &&
                            this.hasMore
                        ) {
                            this.loadMore();
                        }
                    },
                    {rootMargin: "200px"},
                );

                this._observer.observe(sentinel);
            });
        },

        destroy() {
            if (this._observer) this._observer.disconnect();
        },

        async loadMore() {
            this.loading = true;
            this.page++;

            const separator = this.url.includes("?") ? "&" : "?";
            const fetchUrl = `${this.url}${separator}page=${this.page}`;

            try {
                const response = await fetch(fetchUrl, {
                    headers: {"HX-Request": "true"},
                });

                if (!response.ok) {
                    this.hasMore = false;
                    return;
                }

                const html = await response.text();
                if (!html.trim()) {
                    this.hasMore = false;
                    return;
                }

                const target = document.querySelector(this.targetId);
                if (target) {
                    target.insertAdjacentHTML("beforeend", html);
                }
            } catch {
                this.hasMore = false;
            } finally {
                this.loading = false;
            }
        },
    }));
});
