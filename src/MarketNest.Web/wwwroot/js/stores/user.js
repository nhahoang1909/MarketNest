/**
 * Alpine.js user store — hydrated from server-rendered meta tags or data attributes
 */
import { Routes, UserRoles, HtmxConfig } from "../constants.js";

document.addEventListener("alpine:init", () => {
    Alpine.store("user", {
        isAuthenticated: false,
        id: null,
        name: "",
        role: UserRoles.GUEST,

        get isSeller() {
            return this.role === UserRoles.SELLER || this.role === UserRoles.ADMIN;
        },

        get isAdmin() {
            return this.role === UserRoles.ADMIN;
        },

        init() {
            const meta = document.querySelector(HtmxConfig.USER_DATA_META_SELECTOR);
            if (meta) {
                try {
                    const data = JSON.parse(meta.content);
                    this.isAuthenticated = data.isAuthenticated ?? false;
                    this.id = data.id ?? null;
                    this.name = data.name ?? "";
                    this.role = data.role ?? UserRoles.GUEST;
                } catch {
                    /* ignore parse errors */
                }
            }
        },

        logout() {
            this.isAuthenticated = false;
            this.id = null;
            this.name = "";
            this.role = UserRoles.GUEST;
            window.location.href = Routes.AUTH_LOGIN;
        },
    });
});
