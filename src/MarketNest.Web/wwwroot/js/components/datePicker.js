/**
 * Simple date picker Alpine.js component
 * Usage: x-data="datePicker({ value: '2026-04-25' })"
 */
document.addEventListener("alpine:init", () => {
    Alpine.data(
        "datePicker",
        ({value = "", minDate = "", maxDate = ""} = {}) => ({
            value,
            minDate,
            maxDate,
            open: false,
            displayValue: "",

            init() {
                this._formatDisplay();
            },

            setValue(date) {
                this.value = date;
                this._formatDisplay();
                this.open = false;
                this.$dispatch("date-change", {value: date});
            },

            clear() {
                this.value = "";
                this.displayValue = "";
                this.open = false;
                this.$dispatch("date-change", {value: ""});
            },

            _formatDisplay() {
                if (!this.value) {
                    this.displayValue = "";
                    return;
                }
                try {
                    const d = new Date(this.value + "T00:00:00");
                    this.displayValue = d.toLocaleDateString(undefined, {
                        year: "numeric",
                        month: "short",
                        day: "numeric",
                    });
                } catch {
                    this.displayValue = this.value;
                }
            },
        }),
    );
});
