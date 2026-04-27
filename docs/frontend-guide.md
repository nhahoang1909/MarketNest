# MarketNest — Frontend Guide

> Version: 0.1 | Status: Draft | Date: 2026-04
> Consolidated from: `frontend-requirements.md` + `frontend-component-library.md` + `be-fe-common-services.md`
> **Rule: Before building a new UI element — check here first. If it exists, use it.**

---

## Table of Contents

1. [Technology Stack](#1-technology-stack)
2. [Page Inventory](#2-page-inventory)
3. [HTMX Patterns](#3-htmx-patterns)
4. [BE-FE Communication Contract](#4-be-fe-communication-contract)
5. [Component Library](#5-component-library)
6. [Alpine.js Components & Stores](#6-alpinejs-components--stores)
7. [CSS Component Classes](#7-css-component-classes)
8. [Styling & Layout](#8-styling--layout)
9. [Forms & Validation](#9-forms--validation)
10. [Performance & Accessibility](#10-performance--accessibility)
11. [Frontend Dev Tooling](#11-frontend-dev-tooling)

---

## 1. Technology Stack

| Technology | Version | Role |
|------------|---------|------|
| **HTMX** | 2.x | Server-driven partial page updates |
| **Alpine.js** | 3.x | Client-side reactivity (dropdowns, modals, tabs) |
| **Tailwind CSS** | 4.x | Utility-first styling |
| **Razor Pages** | .NET 10 | Server-side rendering, page handlers |
| **Flowbite** | Latest | Tailwind UI component library (optional) |

**Why HTMX + Alpine (not React/Vue)?**
- Stays in Razor Pages model — no API versioning overhead
- HTMX fetches HTML fragments; Alpine fills client-only UI state gaps
- Excellent fit for CRUD-heavy marketplace
- **Limitation**: No offline support, complex animations harder, real-time limited to SSE/polling
- **No bundler needed** — HTMX + Alpine from CDN; Tailwind built via CLI

---

## 2. Page Inventory

### Public Pages

| Page | URL | Key Features |
|------|-----|-------------|
| Home | `/` | Featured storefronts, categories, search |
| Search Results | `/search?q=&category=&sort=` | HTMX filters, infinite scroll |
| Storefront | `/shop/{slug}` | Seller bio, product grid, ratings |
| Product Detail | `/shop/{slug}/products/{productId}` | Variant selector (Alpine), gallery, reviews |
| Cart | `/cart` | Line items, HTMX quantity, TTL countdown (Alpine) |
| Checkout | `/checkout` | Address form, payment (stub) |
| Auth | `/auth/login`, `/auth/register` | Forms + email verify |

### Buyer Dashboard (`/account/...`)

Orders, Order Detail (timeline + dispute), Write Review, Disputes, Account Settings

#### Account Settings (`/account/settings`)

9-tab settings page using HTMX tab switching. Left sidebar on desktop, top tabs on mobile.

| Tab | URL (HTMX partial) | Key Features |
|-----|---------------------|-------------|
| Profile | `/account/settings/profile` | Full name, phone, avatar upload, seller bio |
| Addresses | `/account/settings/addresses` | Address book CRUD, default badge, 10-address limit |
| Preferences | `/account/settings/preferences` | Timezone, time/date format, currency display, preview |
| Communications | `/account/settings/communications` | Notification toggles, frequency, alternate email |
| Security | `/account/settings/security` | Password change (Phase 1), 2FA/sessions (Phase 2) |
| Privacy | `/account/settings/privacy` | Profile visibility, search toggle |
| Shipping | `/account/settings/shipping` | Preferred speed, max cost tolerance (buyer only) |
| Favorites | `/account/settings/favorites` | Followed sellers grid, wishlist product cards |
| Order Preferences | `/account/settings/order-preferences` | Notification delay, dispute preference |

**HTMX Pattern:**
```html
<!-- Tab navigation -->
<nav>
  <a hx-get="/account/settings/profile" hx-target="#settings-content"
     hx-swap="innerHTML" hx-push-url="true">Profile</a>
  <!-- ...repeat for each tab... -->
</nav>
<div id="settings-content">
  <!-- Active tab partial loaded here -->
</div>
```

**Form submission pattern:**
```html
<form hx-post="/account/settings/profile" hx-target="#settings-content" hx-swap="innerHTML">
  <!-- form fields -->
  <button type="submit">Save</button>
</form>
<!-- Server returns: updated partial + HX-Trigger: toastShow -->
```

### Seller Dashboard (`/seller/...`)

Dashboard (Chart.js), Storefront, Products (CRUD), Variant Manager (inline edit), Orders, Payouts, Reviews (reply), Disputes

### Admin Panel (`/admin/...`)

Dashboard (metrics), Users, Storefronts, Disputes Queue, Commission Config, Notifications Log

---

## 3. HTMX Patterns

### Search / Filter (No Page Reload)
```html
<input type="text" name="q"
       hx-get="/search/results"
       hx-trigger="keyup changed delay:400ms"
       hx-target="#product-grid"
       hx-indicator="#search-spinner">
```

### Cart Actions
```html
<!-- Add to cart -->
<button hx-post="/cart/items"
        hx-vals='{"variantId": "{{variantId}}", "qty": 1}'
        hx-target="#cart-summary" hx-swap="outerHTML">Add to Cart</button>

<!-- Remove item -->
<button hx-delete="/cart/items/{{cartItemId}}"
        hx-target="closest tr" hx-swap="outerHTML swap:300ms">Remove</button>
```

### Inline Editing
```html
<td hx-get="/seller/variants/{{id}}/edit"
    hx-trigger="dblclick" hx-target="this" hx-swap="innerHTML">{{quantity}}</td>
```

### Request / Response Conventions

| Action | Method | Response |
|--------|--------|----------|
| Load partial | GET | HTML fragment |
| Form submit | POST | HX-Redirect or success partial |
| Update | PUT/PATCH | Updated fragment |
| Delete | DELETE | Empty + `hx-swap="outerHTML"` |
| Error | Any | `HX-Retarget: #error-region` + error partial |

### HTMX Partial Endpoints

```
GET  /cart/summary                   → _CartSummary partial
GET  /search/results?q=&page=        → _ProductGrid partial
POST /cart/items                     → add item, returns _CartSummary
GET  /seller/products?page=&status=  → _ProductList partial
GET  /disputes/{id}/messages         → polling, returns _MessageList

# Account Settings tabs (each returns a tab partial)
GET  /account/settings/profile            → _SettingsProfile partial
GET  /account/settings/addresses          → _SettingsAddresses partial
GET  /account/settings/preferences        → _SettingsPreferences partial
GET  /account/settings/communications     → _SettingsCommunications partial
GET  /account/settings/security           → _SettingsSecurity partial
GET  /account/settings/privacy            → _SettingsPrivacy partial
GET  /account/settings/shipping           → _SettingsShipping partial
GET  /account/settings/favorites          → _SettingsFavorites partial
GET  /account/settings/order-preferences  → _SettingsOrderPreferences partial
POST /account/settings/{tab}              → save tab, returns updated partial + toast
```

### HTMX Global Config (in _Layout.cshtml)
```html
<meta name="htmx-config" content='{
  "defaultSwapStyle": "outerHTML",
  "globalViewTransitions": true
}'>
<script>
  document.addEventListener('htmx:configRequest', (e) => {
    e.detail.headers['RequestVerificationToken'] =
      document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  });
</script>
```

---

## 4. BE-FE Communication Contract

### HX-Trigger Event Bus

Server fires events via `HX-Trigger` header; Alpine.js listens on `window`:

| Event | Data | Effect |
|-------|------|--------|
| `cartUpdated` | `{ count, total }` | Update cart badge |
| `toastShow` | `{ message, type }` | Show toast notification |
| `modalClose` | `{}` | Close open modal |
| `inventoryLow` | `{ variantId, remaining }` | Stock warning |

```csharp
// Server helper
public static void HxTrigger(this HttpResponse response, string eventName, object? data = null)
{
    var payload = data is null ? $"\"{eventName}\""
        : $"{{\"{eventName}\": {JsonSerializer.Serialize(data)}}}";
    response.Headers["HX-Trigger"] = payload;
}

// Usage
Response.HxTrigger("cartUpdated", new { count = 3, total = "$45.00" });
Response.HxTrigger("toastShow", new { message = "Added to cart!", type = "success" });
```

### Alpine.js Global Stores

```javascript
// Cart Store — initialized from server-rendered data in _Layout.cshtml
Alpine.store('cart', { count: 0, total: 0, reservations: [] });

// Toast Store
Alpine.store('toasts', {
    items: [],
    add(message, type = 'success', duration = 4000) { /* auto-dismiss */ },
    remove(id) { ... }
});

// User Store — UX only, NOT for security
Alpine.store('user', { isAuthenticated: false, id: null, name: '', role: '' });
```

### Currency Formatting
- **Always server-side** — `amount.ToString("C2", CultureInfo)` → `"$12.50"`
- Never format currency in Alpine.js

### Auth State in HTML
```html
<!-- _Layout.cshtml — for Alpine UX only, all auth decisions are server-side -->
@if (User.Identity?.IsAuthenticated == true)
{
  <script>
    document.addEventListener('alpine:init', () => {
      Alpine.store('user', { isAuthenticated: true, id: '@User.GetUserId()', role: '@User.GetRole()' });
    });
  </script>
}
```

---

## 5. Component Library

All shared components in `src/MarketNest.Web/Pages/Shared/`.

```
Pages/Shared/
├── _Layout.cshtml             ← Master (topnav, footer, Alpine/HTMX init)
├── _LayoutSeller.cshtml       ← Seller dashboard (sidebar nav)
├── _LayoutAdmin.cshtml        ← Admin layout
├── _ViewImports.cshtml
├── Forms/
│   ├── _TextField.cshtml          ← Input + label + validation
│   ├── _TextArea.cshtml
│   ├── _SelectField.cshtml
│   ├── _CheckboxField.cshtml
│   ├── _RadioGroup.cshtml
│   ├── _DatePicker.cshtml         ← Alpine date picker
│   ├── _MoneyInput.cshtml         ← Decimal + currency prefix
│   ├── _SearchInput.cshtml        ← HTMX search with debounce
│   ├── _MultiSelect.cshtml        ← Tag-style (Alpine)
│   ├── _ImageUpload.cshtml        ← Drag-drop with preview
│   └── _FormSection.cshtml
├── Display/
│   ├── _StatusBadge.cshtml        ← Colored per domain
│   ├── _StarRating.cshtml         ← Display 0–5
│   ├── _StarRatingInput.cshtml    ← Interactive (Alpine)
│   ├── _Avatar.cshtml, _EmptyState.cshtml, _LoadingSpinner.cshtml
│   ├── _Alert.cshtml, _Breadcrumb.cshtml, _PriceDisplay.cshtml
├── Navigation/
│   ├── _Pagination.cshtml         ← HTMX page nav
│   ├── _Tabs.cshtml              ← Alpine tab state
│   └── _SidebarNav.cshtml
├── Data/
│   ├── _DataTable.cshtml          ← Sortable table + HTMX
│   ├── _FilterBar.cshtml          ← Filter chips
│   └── _SortHeader.cshtml         ← Clickable column sort
├── Overlays/
│   ├── _Modal.cshtml, _ConfirmDialog.cshtml, _Drawer.cshtml, _Toast.cshtml
└── Domain/
    ├── _ProductCard.cshtml, _OrderStatusBadge.cshtml
    ├── _CartSummaryBadge.cshtml, _StoreCard.cshtml, _ReviewCard.cshtml
```

### Layouts

Layouts live alongside components in `Pages/Shared/`:

```
_Layout.cshtml         ← Master (topnav, footer, Alpine/HTMX init)
_LayoutSeller.cshtml   ← Seller dashboard (sidebar nav)
_LayoutAdmin.cshtml    ← Admin layout
```

### Partial Naming Convention

```
_{Module}{ViewName}{Modifier}.cshtml
Examples: _OrderRow, _ProductCard, _CartSummary, _ReviewFormError, _DisputeMessageThread
```

---

## 6. Alpine.js Components & Stores

Scripts in `wwwroot/js/`:

```
js/
├── app.js                  ← Entry: Alpine.start(), stores, magic helpers
├── components/
│   ├── datePicker.js, multiSelect.js, imageUploader.js
│   ├── starRating.js, confirmDialog.js, reservationTimer.js, infiniteScroll.js
├── stores/
│   ├── cart.js, toasts.js, user.js
└── magic/
    └── htmxHelpers.js      ← $htmx magic
```

### Magic Helpers

```javascript
Alpine.magic('currency', () => (amount) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount));
Alpine.magic('date', () => (iso) => new Date(iso).toLocaleDateString('en-US', { ... }));
Alpine.magic('timeAgo', () => (iso) => { /* seconds/minutes/hours/days ago */ });
```

### Key Components

**Star Rating Input:**
```javascript
Alpine.data('starRating', (initialValue = 0) => ({
    value: initialValue, hover: 0,
    get stars() { return [1,2,3,4,5].map(n => ({ n, filled: n <= (this.hover || this.value) })); },
    setHover(n) { this.hover = n; }, clearHover() { this.hover = 0; }, select(n) { this.value = n; }
}));
```

**Image Uploader:**
```javascript
Alpine.data('imageUploader', ({ maxFiles = 3, maxSizeMb = 5 } = {}) => ({
    files: [], previews: [], isDragging: false, error: '',
    handleFiles(event) { this.addFiles(Array.from(event.target.files)); },
    handleDrop(event) { this.isDragging = false; this.addFiles(Array.from(event.dataTransfer.files)); },
    addFiles(incoming) { /* validate size/type, read previews, enforce maxFiles */ },
    removeFile(index) { this.files.splice(index, 1); this.previews.splice(index, 1); }
}));
```

**Reservation TTL Countdown:**
```html
<div x-data="{ ttl: 900, interval: null }"
     x-init="interval = setInterval(() => { if(ttl > 0) ttl--; else clearInterval(interval); }, 1000)">
  <span x-text="`Reserved: ${Math.floor(ttl/60)}:${String(ttl%60).padStart(2,'0')}`"></span>
  <template x-if="ttl < 120"><p class="text-red-500">Cart expiring soon!</p></template>
</div>
```

---

## 7. CSS Component Classes

Defined in `wwwroot/css/components.css` with `@layer components`:

```css
/* Buttons */
.btn-primary   { @apply bg-brand-500 text-white rounded-lg hover:bg-brand-600 ... }
.btn-secondary { @apply bg-white text-gray-700 border rounded-lg hover:bg-gray-50 ... }
.btn-danger    { @apply bg-red-600 text-white rounded-lg hover:bg-red-700 ... }
.btn-ghost     { @apply text-gray-600 rounded-lg hover:bg-gray-100 ... }

/* Forms */
.form-label  { @apply text-sm font-medium text-gray-700 mb-1 ... }
.form-input  { @apply rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 ... }
.input-error { @apply border-red-500 focus:ring-red-500 ... }
.form-error  { @apply text-xs text-red-600 mt-1 ... }

/* Cards */
.card        { @apply bg-white rounded-xl border shadow-sm ... }
.card-header { @apply px-6 py-4 border-b ... }

/* Badges */
.badge-green  { @apply bg-green-100 text-green-700 rounded-full text-xs px-2 py-0.5 ... }
.badge-red    { @apply bg-red-100 text-red-700 ... }
/* + gray, blue, yellow, orange, purple */

/* Filter chips */
.filter-chip        { @apply px-3 py-1 rounded-full text-xs border ... }
.filter-chip-active { @apply bg-brand-500 text-white border-brand-500 ... }
```

### Status Badge Mapping

```csharp
public static class StatusBadgeHelper
{
    public static string GetCssClass(string status) => status switch
    {
        "Pending" => "badge-yellow", "Confirmed" | "Processing" | "Delivered" => "badge-blue",
        "Shipped" => "badge-purple", "Completed" | "Active" | "Paid" => "badge-green",
        "Cancelled" | "Refunded" | "Failed" | "Closed" | "Clawback" => "badge-red",
        "Disputed" | "Suspended" => "badge-orange", "Draft" => "badge-gray",
        _ => "badge-gray"
    };
}
```

---

## 8. Styling & Layout

### Design Tokens (Tailwind)

```javascript
colors: {
  brand: { 50: '#fef7f0', 500: '#f97316', 900: '#431407' },
  surface: { DEFAULT: '#ffffff', muted: '#f9fafb', subtle: '#f3f4f6' }
},
fontFamily: { sans: ['Inter var', 'system-ui', 'sans-serif'] }
```

### Layout Structure

```
┌─────────────────────────────────────────────┐
│  TopBar: Logo | Search | Cart | User Menu   │
│  CategoryNav: Browse categories             │
├─────────────────────────────────────────────┤
│  [sidebar?]     Main Content Area           │
├─────────────────────────────────────────────┤
│  Footer: Links | Social | Newsletter        │
└─────────────────────────────────────────────┘
```

### Responsive
- Mobile-first: `sm:640`, `md:768`, `lg:1024`, `xl:1280`
- Product grid: 1→2→3→4 cols
- Seller dashboard: drawer (mobile) → fixed sidebar (lg+)

### Page Data Convention

```csharp
public abstract class BasePageModel : PageModel
{
    [ViewData] public string PageTitle { get; set; } = "MarketNest";
    [ViewData] public BreadcrumbItem[] Breadcrumbs { get; set; } = [];
    protected void SetTitle(string title) => PageTitle = $"{title} — MarketNest";
}
```

---

## 9. Forms & Validation

### Strategy: Server-First + HTMX Enhancement

1. All validation in domain/application layer
2. Server returns form partial with inline errors on failure
3. HTML5 native validation as first-pass client-side
4. No duplicated validation in Alpine — Alpine handles only UX state

### Anti-CSRF
- All forms include `@Html.AntiForgeryToken()`
- HTMX includes token via `htmx:configRequest` event handler

### Form Field Names — PascalCase

```html
<input name="ShippingAddress.Street" />
<input name="ShippingAddress.City" />
```

### Error Handling

```csharp
if (!ModelState.IsValid)
{
    Response.Headers["HX-Retarget"] = "#form-error";
    Response.Headers["HX-Reswap"] = "outerHTML";
    return Partial("_FormErrors", ModelState);
}
```

---

## 10. Performance & Accessibility

### Performance Targets

| Metric | Target | Method |
|--------|--------|--------|
| TTFB | < 200ms | SSR |
| LCP | < 2.5s | Lazy loading, CDN |
| CLS | < 0.1 | Reserved image space |
| JS bundle | < 50KB gzip | HTMX ~14KB + Alpine ~10KB |

### Loading Strategy

Full strategy documented in `docs/loading-strategy.md`. Summary of technique selection:

| Technique | When to use |
|-----------|-------------|
| **SSR (no loading)** | LCP elements — hero, title, price, cart items |
| **Skeleton loading** | Above-fold content that fetches from DB asynchronously via HTMX |
| **Lazy load (HTMX `intersect once`)** | Below-fold content — reviews, related products, chart |
| **HTMX indicator / spinner** | Any user-triggered action (form submit, filter, pagination) |
| **Alpine loading state** | Multi-step flows, button state during form POST |

**Decision rule:**
```
LCP element?          → SSR immediately, no skeleton
DB data, above fold?  → Skeleton (hx-trigger="load") 
Below fold?           → Lazy (hx-trigger="intersect once")
User action?          → HTMX indicator or Alpine spinner
Transaction page?     → SSR max + spinner on submit action only
```

**Phase 1 foundation (implemented 2026-04-27):**
- `.skeleton-shimmer` CSS class (gradient sweep animation) + 4 skeleton shape classes in `components.css`
- Reusable skeleton partials: `_SkeletonProductCard`, `_SkeletonStoreCard`, `_SkeletonOrderRow`, `_SkeletonStatCard`
- All image components (`_ProductCard`, `_StoreCard`, `_Avatar`, `_ReviewCard`) have explicit `width`/`height` + `loading="lazy"` → CLS fixed
- Checkout: Alpine `submitting` state + full-page processing overlay on order submit
- `_SearchInput`: inline spinner inside input field
- `_FilterBar` / `_Pagination`: optional `IndicatorId` ViewData param → `hx-indicator`

**Phase 2 (per page, when real DB data is connected):**
Each page that integrates a real data query should add skeleton loading at that point. Use the existing skeleton partials as the placeholder content in `hx-trigger="load"` sections.

### Image Rules

- `loading="eager"` + `fetchpriority="high"` → LCP images only (hero banner, above-fold store banner)
- `loading="lazy"` + explicit `width` + `height` → all other images (product cards, avatars, thumbnails)
- Always set `width`/`height` or `aspect-ratio` — prevents CLS while image loads
- Background images set via CSS: no `loading` attribute, use `contain-intrinsic-size` if needed

### HTMX Indicator Pattern

Every HTMX request must have a visible loading state. Use the appropriate level:

```html
<!-- Small inline spinner for quick actions (< 1s expected) -->
<button hx-post="/cart/items" hx-indicator="#cart-spinner">
  Add to cart
  <span id="cart-spinner" class="htmx-indicator">
    <svg class="animate-spin h-4 w-4">...</svg>
  </span>
</button>

<!-- Skeleton overlay for content replacement (1-3s expected) -->
<div id="product-grid" hx-get="/shop/products" hx-trigger="load" hx-swap="outerHTML">
  @await Html.PartialAsync("Display/_SkeletonProductCard",
    new ViewDataDictionary(ViewData) { ["Count"] = 8, ["Cols"] = "4" })
</div>

<!-- Full-page overlay for transaction submit -->
<div x-show="submitting" class="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
  <div class="bg-white rounded-2xl p-8 flex flex-col items-center gap-4">
    <svg class="animate-spin h-10 w-10 text-accent-400">...</svg>
    <p class="font-semibold text-ink-700">Dang xu ly...</p>
  </div>
</div>
```

### Caching
- Catalog pages: `public, max-age=60`
- Cart/account: `private, no-cache`
- Static assets: `public, max-age=31536000, immutable`

### Accessibility (WCAG 2.2 AA)
- All interactive elements keyboard-navigable
- Focus traps for modals
- ARIA labels, `aria-live` regions for HTMX updates
- Color contrast ≥ 4.5:1
- Skeleton elements use `aria-hidden="true"` + `aria-label="Loading..."` on wrapper

---

## 11. Frontend Dev Tooling

```
Node.js     — Tailwind CSS build only
Tailwind    — npx tailwindcss build (watch mode in dev)
Playwright  — E2E tests (Phase 2+)
```

No bundler (Webpack/Vite) — HTMX + Alpine from CDN in dev, copied to wwwroot for prod.

---

## Appendix: Custom Tag Helpers

```csharp
// mn-confirm: Custom confirm dialog before HTMX action
[HtmlTargetElement("*", Attributes = "mn-confirm")]
public class HtmxConfirmTagHelper : TagHelper { ... }

// mn-nav: Adds active CSS class when URL matches current page
[HtmlTargetElement("a", Attributes = "mn-nav")]
public class ActiveNavTagHelper : TagHelper { ... }
```

