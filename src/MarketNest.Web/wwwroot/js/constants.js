/**
 * MarketNest — Centralized JS constants
 * No magic strings or magic numbers — all repeated values live here.
 */

// ── Routes ───────────────────────────────────────────────────────────
export const Routes = Object.freeze({
    AUTH_LOGIN: "/auth/login",
    HOME: "/",
});

// ── User Roles ───────────────────────────────────────────────────────
export const UserRoles = Object.freeze({
    GUEST: "guest",
    BUYER: "buyer",
    SELLER: "seller",
    ADMIN: "admin",
});

// ── Toast Configuration ──────────────────────────────────────────────
export const ToastConfig = Object.freeze({
    DEFAULT_DURATION_MS: 5000,
    DOM_REMOVAL_DELAY_MS: 300,
    DEFAULT_TYPE: "info",
});

// ── Currency Formatting ──────────────────────────────────────────────
export const CurrencyDefaults = Object.freeze({
    CURRENCY: "USD",
    LOCALE: "en-US",
    MIN_FRACTION_DIGITS: 2,
});

// ── Time Intervals (seconds) ─────────────────────────────────────────
export const TimeIntervals = Object.freeze({
    YEAR: 31536000,
    MONTH: 2592000,
    WEEK: 604800,
    DAY: 86400,
    HOUR: 3600,
    MINUTE: 60,
});

// ── Timer ────────────────────────────────────────────────────────────
export const TimerConfig = Object.freeze({
    TICK_INTERVAL_MS: 1000,
    MS_PER_MINUTE: 60000,
    MS_PER_SECOND: 1000,
    PLACEHOLDER: "--:--",
    EXPIRED: "00:00",
});

// ── Infinite Scroll ──────────────────────────────────────────────────
export const InfiniteScrollConfig = Object.freeze({
    ROOT_MARGIN: "200px",
    HX_REQUEST_HEADER: "HX-Request",
});

// ── Image Upload ─────────────────────────────────────────────────────
export const ImageUploadConfig = Object.freeze({
    DEFAULT_MAX_FILES: 5,
    IMAGE_MIME_PREFIX: "image/",
});

// ── Rich Text Editor ─────────────────────────────────────────────────
export const RichEditorConfig = Object.freeze({
    DEFAULT_MAX_LENGTH: 20000,
    MAX_IMAGE_SIZE_BYTES: 2 * 1024 * 1024,       // 2 MB — matches FieldLimits.RichEditorImage.MaxSizeBytes
    MAX_IMAGE_SIZE_MB: 2,
    MAX_IMAGES_PER_DOCUMENT: 10,
    ALLOWED_IMAGE_TYPES: Object.freeze(["image/jpeg", "image/png", "image/webp", "image/gif"]),
    UPLOAD_URL: "/api/v1/uploads/rich-editor-image",
    CHAR_WARNING_THRESHOLD: 0.8,                  // show amber at 80% capacity
});

// ── HTMX ─────────────────────────────────────────────────────────────
export const HtmxConfig = Object.freeze({
    REQUEST_CLASS: "htmx-request",
    CSRF_INPUT_SELECTOR: 'input[name="__RequestVerificationToken"]',
    CSRF_META_SELECTOR: 'meta[name="csrf-token"]',
    CSRF_HEADER: "RequestVerificationToken",
    USER_DATA_META_SELECTOR: 'meta[name="user-data"]',
});

// ── HTTP Status Codes ────────────────────────────────────────────────
export const HttpStatus = Object.freeze({
    UNAUTHORIZED: 401,
    INTERNAL_SERVER_ERROR: 500,
});

// ── Error Messages ───────────────────────────────────────────────────
export const ErrorMessages = Object.freeze({
    GENERIC: "Something went wrong. Please try again.",
});

// ── Display Strings ──────────────────────────────────────────────────
export const DisplayStrings = Object.freeze({
    JUST_NOW: "just now",
});

