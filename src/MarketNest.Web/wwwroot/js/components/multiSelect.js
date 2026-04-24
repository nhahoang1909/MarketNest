/**
 * Tag-style multi-select Alpine.js component
 * Usage: x-data="multiSelect({ options: [...], selected: [...] })"
 */
document.addEventListener("alpine:init", () => {
    Alpine.data(
        "multiSelect",
        ({options = [], selected = [], name = "tags"} = {}) => ({
            options,
            selected,
            name,
            search: "",
            open: false,

            get filteredOptions() {
                const q = this.search.toLowerCase();
                return this.options.filter(
                    (opt) =>
                        !this.selected.includes(opt.value) &&
                        opt.label.toLowerCase().includes(q),
                );
            },

            get selectedLabels() {
                return this.selected.map(
                    (val) =>
                        this.options.find((o) => o.value === val)?.label ?? val,
                );
            },

            toggle(value) {
                const idx = this.selected.indexOf(value);
                if (idx === -1) {
                    this.selected.push(value);
                } else {
                    this.selected.splice(idx, 1);
                }
                this.search = "";
                this.$dispatch("multiselect-change", {
                    selected: [...this.selected],
                });
            },

            remove(value) {
                this.selected = this.selected.filter((v) => v !== value);
                this.$dispatch("multiselect-change", {
                    selected: [...this.selected],
                });
            },

            isSelected(value) {
                return this.selected.includes(value);
            },
        }),
    );
});
