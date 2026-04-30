/**
 * Rich text editor Alpine.js component — wraps Trix editor
 * Usage: x-data="richEditor({ fieldId: 'desc', maxLength: 20000, uploadUrl: '/api/...' })"
 *
 * Requires Trix CSS + JS to be loaded on the page via @section Scripts.
 */
import {HtmxConfig, RichEditorConfig} from "../constants.js";

document.addEventListener("alpine:init", () => {
    Alpine.data("richEditor", ({
        fieldId = "editor",
        maxLength = RichEditorConfig.DEFAULT_MAX_LENGTH,
        initialHtml = "",
        uploadUrl = RichEditorConfig.UPLOAD_URL,
    } = {}) => ({
        htmlContent: initialHtml,
        charCount: 0,
        hasError: false,
        errorMessage: "",
        isUploading: false,
        uploadError: "",

        init() {
            const editorEl = document.getElementById(fieldId);
            if (!editorEl) return;

            if (initialHtml) {
                editorEl.addEventListener("trix-initialize", () => {
                    editorEl.editor.loadHTML(initialHtml);
                    this._syncCharCount();
                }, {once: true});
            }

            this._syncCharCount();
        },

        onContentChange(event) {
            this.htmlContent = event.target.value;
            this._syncCharCount();
            this._validate();
        },

        onFileAccept(event) {
            const file = event.file;

            if (!RichEditorConfig.ALLOWED_IMAGE_TYPES.includes(file.type)) {
                event.preventDefault();
                this.uploadError = "Only JPEG, PNG, WebP, and GIF images are allowed.";
                return;
            }

            if (file.size > RichEditorConfig.MAX_IMAGE_SIZE_BYTES) {
                event.preventDefault();
                const sizeMb = (file.size / 1024 / 1024).toFixed(1);
                this.uploadError = `Image must be smaller than ${RichEditorConfig.MAX_IMAGE_SIZE_MB}MB. This file is ${sizeMb}MB.`;
                return;
            }

            this.uploadError = "";
        },

        async onAttachmentAdd(event) {
            const attachment = event.attachment;
            if (!attachment.file) return;

            this.isUploading = true;
            this.uploadError = "";

            try {
                const formData = new FormData();
                formData.append("file", attachment.file);

                const token = this._getAntiForgeryToken();
                const headers = {"X-Requested-With": "XMLHttpRequest"};
                if (token) {
                    headers[HtmxConfig.CSRF_HEADER] = token;
                }

                const response = await fetch(uploadUrl, {
                    method: "POST",
                    body: formData,
                    headers,
                });

                if (!response.ok) {
                    const err = await response.json().catch(() => ({}));
                    throw new Error(err.detail || "Upload failed. Please try again.");
                }

                const {url, fileId} = await response.json();

                attachment.setAttributes({
                    url,
                    href: url,
                    "data-file-id": fileId,
                });
            } catch (err) {
                this.uploadError = err.message;
                attachment.remove();
            } finally {
                this.isUploading = false;
            }
        },

        _syncCharCount() {
            this.charCount = (this.htmlContent || "").length;
        },

        _validate() {
            if (this.charCount > maxLength) {
                this.hasError = true;
                this.errorMessage = `Content must not exceed ${maxLength.toLocaleString()} characters. Currently: ${this.charCount.toLocaleString()}.`;
            } else {
                this.hasError = false;
                this.errorMessage = "";
            }
        },

        _getAntiForgeryToken() {
            return document.querySelector(HtmxConfig.CSRF_INPUT_SELECTOR)?.value
                ?? document.querySelector(HtmxConfig.CSRF_META_SELECTOR)?.content
                ?? "";
        },
    }));
});

