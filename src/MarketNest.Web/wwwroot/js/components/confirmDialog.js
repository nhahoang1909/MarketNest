/**
 * Confirm dialog Alpine.js component
 * Usage: x-data="confirmDialog()"
 * Dispatch: $dispatch('confirm-dialog', { title, message, onConfirm })
 */
document.addEventListener("alpine:init", () => {
    Alpine.data("confirmDialog", () => ({
        open: false,
        title: "Are you sure?",
        message: "This action cannot be undone.",
        _onConfirm: null,
        _onCancel: null,

        show(detail) {
            this.title = detail?.title ?? "Are you sure?";
            this.message = detail?.message ?? "This action cannot be undone.";
            this._onConfirm = detail?.onConfirm ?? null;
            this._onCancel = detail?.onCancel ?? null;
            this.open = true;
        },

        confirm() {
            if (typeof this._onConfirm === "function") {
                this._onConfirm();
            }
            this.open = false;
        },

        cancel() {
            if (typeof this._onCancel === "function") {
                this._onCancel();
            }
            this.open = false;
        },
    }));
});
