# MarketNest — Frontend Requirements

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04

---

## 1. Technology Stack

| Technology | Version | Role |
|------------|---------|------|
| **HTMX** | 2.x | Server-driven partial page updates |
| **Alpine.js** | 3.x | Client-side reactivity (dropdowns, modals, tabs) |
| **Tailwind CSS** | 4.x | Utility-first styling |
| **Razor Pages** | .NET 10 | Server-side rendering, page handlers |
| **Flowbite** | Latest | Tailwind UI component library (optional) |

### Why HTMX + Alpine (not React/Vue)?
- Stays in Razor Pages mental model — no API versioning overhead
- Server handles all business logic; HTMX fetches HTML fragments
- Alpine.js fills the gap for purely client-side UI state (no server round-trip needed)
- Excellent fit for CRUD-heavy marketplace workflows
- **Limitation to accept**: No offline support, complex animations harder, real-time limited to SSE/polling

---

## 2. Page Inventory

### 2.1 Public Pages (Guest + Buyer)

| Page | URL Pattern | Key Features |
|------|------------|--------------|
| Home / Browse | `/` | Featured storefronts, category navigation, search bar |
| Search Results | `/search?q=&category=&sort=` | HTMX-driven filters (no page reload), infinite scroll |
| Storefront Page | `/shop/{slug}` | Seller bio, product grid, ratings summary |
| Product Detail | `/shop/{slug}/products/{productId}` | Variant selector (Alpine), image gallery, reviews, add-to-cart |
| Cart | `/cart` | Line items, quantity adjust (HTMX), reservation TTL countdown (Alpine) |
| Checkout | `/checkout` | Address form, order summary, payment (stubbed) |
| Order Confirmation | `/orders/{orderId}/confirmation` | Thank-you page |
| Login / Register | `/auth/login`, `/auth/register` | Standard forms + email verify flow |
| Forgot Password | `/auth/forgot-password` | Email + reset token flow |

### 2.2 Buyer Dashboard (Authenticated)

| Page | URL Pattern | Key Features |
|------|------------|--------------|
| My Orders | `/account/orders` | Order list with status badges, HTMX search/filter |
| Order Detail | `/account/orders/{orderId}` | Timeline, fulfillment tracking, dispute button |
| Write Review | `/account/orders/{orderId}/review` | Star rating (Alpine), text + photo upload |
| My Disputes | `/account/disputes` | Dispute list, status |
| Dispute Detail | `/account/disputes/{disputeId}` | Message thread (HTMX polling), evidence upload |
| Account Settings | `/account/settings` | Profile, address book, password change |

### 2.3 Seller Dashboard (Authenticated + Seller Role)

| Page | URL Pattern | Key Features |
|------|------------|--------------|
| Seller Overview | `/seller/dashboard` | Revenue chart (Alpine + Chart.js), pending orders, low stock alerts |
| My Storefront | `/seller/storefront` | Edit storefront profile, banner upload |
| Products | `/seller/products` | Product list with status filter, HTMX pagination |
| Product Editor | `/seller/products/new`, `/seller/products/{id}/edit` | Multi-step form: details → variants → inventory |
| Variant Manager | `/seller/products/{id}/variants` | Add/remove variants, price + stock inline edit (HTMX) |
| Orders | `/seller/orders` | Incoming orders, filter by status |
| Order Detail | `/seller/orders/{orderId}` | Confirm/cancel, add tracking number |
| Payouts | `/seller/payouts` | Payout history, upcoming payout preview |
| Reviews | `/seller/reviews` | Reviews list, reply form (HTMX) |
| Disputes | `/seller/disputes` | Open disputes, respond form |

### 2.4 Admin Panel

| Page | URL Pattern | Key Features |
|------|------------|--------------|
| Admin Overview | `/admin/dashboard` | Platform metrics |
| Users | `/admin/users` | Search, suspend, role assignment |
| Storefronts | `/admin/storefronts` | Approve, suspend storefronts |
| Disputes Queue | `/admin/disputes` | Filter by status, arbitrate |
| Commission Config | `/admin/config/commission` | Per-seller rate override |
| Notifications Log | `/admin/notifications` | Delivery status log |

---

## 3. HTMX Usage Patterns

### 3.1 Search / Filter (No Page Reload)
```html
<!-- Product search with debounce -->
<input type="text" 
       name="q"
       hx-get="/search/results" 
       hx-trigger="keyup changed delay:400ms"
       hx-target="#product-grid"
       hx-indicator="#search-spinner">

<div id="product-grid">
  <!-- Server renders product cards partial -->
</div>
```

### 3.2 Cart Actions
```html
<!-- Add to cart -->
<button hx-post="/cart/items"
        hx-vals='{"variantId": "{{variantId}}", "qty": 1}'
        hx-target="#cart-summary"
        hx-swap="outerHTML">
  Add to Cart
</button>

<!-- Remove item -->
<button hx-delete="/cart/items/{{cartItemId}}"
        hx-target="closest tr"
        hx-swap="outerHTML swap:300ms">
  Remove
</button>
```

### 3.3 Inline Editing (Seller Inventory)
```html
<!-- Inline stock quantity edit -->
<td hx-get="/seller/variants/{{id}}/edit"
    hx-trigger="dblclick"
    hx-target="this"
    hx-swap="innerHTML">
  {{quantity}}
</td>
```

### 3.4 Reservation TTL Countdown (Alpine.js)
```html
<div x-data="{ ttl: 900, interval: null }"
     x-init="interval = setInterval(() => { if(ttl > 0) ttl--; else clearInterval(interval); }, 1000)">
  <span x-text="`Reserved for: ${Math.floor(ttl/60)}:${String(ttl%60).padStart(2,'0')}`"></span>
  <template x-if="ttl < 120">
    <p class="text-red-500">Cart expiring soon!</p>
  </template>
</div>
```

### 3.5 Toast Notifications (Alpine.js)
```html
<!-- Global toast store -->
<div x-data="$store.toasts" 
     x-on:htmx:after-request.window="handleHtmxResponse($event)">
  <template x-for="toast in toasts">
    <div x-text="toast.message" :class="toast.type === 'error' ? 'bg-red-500' : 'bg-green-500'">
    </div>
  </template>
</div>
```

---

## 4. Component Library (Reusable Partials)

### Razor Partial Conventions
All shared UI lives in `Views/Shared/Components/`:

```
Views/Shared/Components/
├── _ProductCard.cshtml        ← Product tile (used in search + storefront)
├── _OrderStatusBadge.cshtml   ← Colored status badge
├── _StarRating.cshtml         ← Display-only stars
├── _StarRatingInput.cshtml    ← Interactive star selector (Alpine)
├── _Pagination.cshtml         ← HTMX-compatible pagination links
├── _CartSummary.cshtml        ← Mini cart count + total in header
├── _Alert.cshtml              ← Success / error / warning alerts
├── _Modal.cshtml              ← Alpine.js modal wrapper
├── _ImageUpload.cshtml        ← Drag-drop upload with preview (Alpine)
├── _BreadcrumbNav.cshtml      ← Dynamic breadcrumbs
└── _DataTable.cshtml          ← Sortable/filterable table (HTMX)
```

---

## 5. Styling Guidelines

### 5.1 Design Tokens (Tailwind config)
```javascript
// tailwind.config.js — MarketNest brand colors
module.exports = {
  theme: {
    extend: {
      colors: {
        brand: {
          50:  '#fef7f0',
          500: '#f97316',  // Primary orange (marketplace feel)
          900: '#431407',
        },
        surface: {
          DEFAULT: '#ffffff',
          muted:   '#f9fafb',
          subtle:  '#f3f4f6',
        }
      },
      fontFamily: {
        sans: ['Inter var', 'system-ui', 'sans-serif'],
      }
    }
  }
}
```

### 5.2 Layout Structure
```
┌─────────────────────────────────────────────┐
│  TopBar: Logo | Search | Cart | User Menu   │
│  CategoryNav: Browse categories             │
├─────────────────────────────────────────────┤
│                                             │
│  [sidebar?]     Main Content Area           │
│                                             │
├─────────────────────────────────────────────┤
│  Footer: Links | Social | Newsletter        │
└─────────────────────────────────────────────┘
```

### 5.3 Responsive Breakpoints
- Mobile-first: `sm:` 640px, `md:` 768px, `lg:` 1024px, `xl:` 1280px
- Product grid: 1 col (mobile) → 2 col (sm) → 3 col (md) → 4 col (xl)
- Seller dashboard: drawer sidebar on mobile, fixed sidebar on lg+

---

## 6. Performance Requirements

| Metric | Target | Method |
|--------|--------|--------|
| TTFB (Time To First Byte) | < 200ms | Server-side rendering |
| LCP (Largest Contentful Paint) | < 2.5s | Image lazy loading, CDN |
| CLS (Cumulative Layout Shift) | < 0.1 | Reserve space for images |
| JS bundle size | < 50KB gzipped | HTMX (~14KB) + Alpine (~10KB) |
| Image optimization | WebP + responsive srcset | Phase 2 |

### Caching Strategy
- Product catalog pages: cache-control `public, max-age=60` (1 min)
- Product detail: `public, max-age=30` with ETag
- Cart / account pages: `private, no-cache`
- Static assets: `public, max-age=31536000, immutable` (with hash in filename)

---

## 7. Accessibility Requirements

- WCAG 2.2 AA compliance target
- All interactive elements keyboard-navigable
- Focus management for modals (Alpine: focus trap)
- ARIA labels on all icon-only buttons
- Color contrast ratio ≥ 4.5:1 for text
- Screen reader announcements for HTMX updates (use `aria-live` regions)

```html
<!-- HTMX ARIA live region for dynamic updates -->
<div id="product-grid" 
     aria-live="polite" 
     aria-atomic="false">
  <!-- Products injected here -->
</div>
```

---

## 8. Forms & Validation

### Strategy: Server-First Validation with HTMX Enhancement

1. All validation rules in domain/application layer
2. Server returns form partial with inline errors on failure
3. HTML5 native validation as first-pass client-side (required, min, max, pattern)
4. No duplicate validation logic in Alpine — Alpine handles only UX state (show/hide, tabs)

### Anti-CSRF
- All POST/PUT/DELETE forms include `@Html.AntiForgeryToken()`
- HTMX requests include header: `hx-headers='{"RequestVerificationToken": "{{token}}"}'`

---

## 9. HTMX Request Conventions

| Action | HTTP Method | Response |
|--------|------------|----------|
| Load partial | GET | HTML fragment |
| Form submit (create) | POST | Redirect (HX-Redirect header) OR success partial |
| Update (inline edit) | PUT/PATCH | Updated HTML fragment |
| Delete | DELETE | Empty response + `hx-swap="outerHTML"` removes element |
| Error | Any | Server returns `HX-Retarget: #error-region` + error partial |

### Error Handling Pattern
```csharp
// In PageModel handler
if (!ModelState.IsValid)
{
    Response.Headers["HX-Retarget"] = "#form-error";
    Response.Headers["HX-Reswap"] = "outerHTML";
    return Partial("_FormErrors", ModelState);
}
```

---

## 10. Frontend Dev Tooling

```
Node.js     — Tailwind CSS build only (no FE framework)
Tailwind    — npx tailwindcss build (watch mode in dev)
BrowserSync — Live reload for Razor changes (dev)
Playwright  — E2E tests (Phase 2+)
```

**No bundler (Webpack/Vite) needed** — HTMX + Alpine served directly from CDN in dev; copied to wwwroot for prod.
