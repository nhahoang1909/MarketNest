/**
 * Image uploader Alpine.js component
 * Usage: x-data="imageUploader({ maxFiles: 5 })"
 */
import {ImageUploadConfig} from "../constants.js";

document.addEventListener("alpine:init", () => {
    Alpine.data("imageUploader", ({maxFiles = ImageUploadConfig.DEFAULT_MAX_FILES} = {}) => ({
        files: [],
        previews: [],
        isDragging: false,
        maxFiles,

        handleFiles(event) {
            const newFiles = Array.from(event.target.files);
            this._addFiles(newFiles);
            event.target.value = "";
        },

        handleDrop(event) {
            this.isDragging = false;
            const newFiles = Array.from(event.dataTransfer.files).filter((f) =>
                f.type.startsWith(ImageUploadConfig.IMAGE_MIME_PREFIX),
            );
            this._addFiles(newFiles);
        },

        removeFile(index) {
            this.files.splice(index, 1);
            this.previews.splice(index, 1);
        },

        _addFiles(newFiles) {
            const remaining = this.maxFiles - this.files.length;
            const toAdd = newFiles.slice(0, remaining);

            toAdd.forEach((file) => {
                this.files.push(file);
                const reader = new FileReader();
                reader.onload = (e) => {
                    this.previews.push(e.target.result);
                };
                reader.readAsDataURL(file);
            });
        },
    }));
});
