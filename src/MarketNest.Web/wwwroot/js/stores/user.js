/**
 * Alpine.js user store — hydrated from server-rendered meta tags or data attributes
 */
document.addEventListener("alpine:init", () => {
    Alpine.store("user", {
        isAuthenticated: false,
        id: null,
        name: "",
        role: "guest",

        get isSeller() {
            return this.role === "seller" || this.role === "admin";
        },

        get isAdmin() {
            return this.role === "admin";
        },

        init() {
            const meta = document.querySelector('meta[name="user-data"]');
            if (meta) {
                try {
                    const data = JSON.parse(meta.content);
                    this.isAuthenticated = data.isAuthenticated ?? false;
                    this.id = data.id ?? null;
                    this.name = data.name ?? "";
                    this.role = data.role ?? "guest";
                } catch {
                    /* ignore parse errors */
                }
            }
        },
    });
});
