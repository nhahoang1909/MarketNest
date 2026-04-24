# MarketNest — BE-FE Common Services & Contracts

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04  
> Defines the shared contracts, response formats, and conventions between Backend (Razor Pages/API) and Frontend (HTMX/Alpine.js)

---

## 1. Overview

In the HTMX + Razor Pages stack, the "API" between BE and FE is **HTML over the wire**, not JSON. However, shared conventions still exist:

- **Server-sent state**: what data the server embeds in HTML
- **HTMX response headers**: how server signals FE actions
- **Alpine.js store contracts**: what client state shape looks like
- **Form contracts**: field names, validation error formats
- **Event conventions**: what HTMX events fire and when

---

## 2. HTMX Request / Response Contract

### 2.1 Standard Request Headers (all HTMX requests)
```
HX-Request: true
HX-Current-URL: https://marketnest.com/cart
HX-Target: #product-grid        (element being updated)
HX-Trigger: btn-add-to-cart     (element that triggered)
HX-Trigger-Name: addToCart      (name attribute if form)
RequestVerificationToken: {{antiforgery-token}}
```

### 2.2 Standard Response Headers (server → FE)

| Header | Value | Effect |
|--------|-------|--------|
| `HX-Redirect` | `/orders/123/confirmation` | Full page redirect |
| `HX-Refresh` | `true` | Force full page reload |
| `HX-Retarget` | `#error-banner` | Override target for this response |
| `HX-Reswap` | `outerHTML` / `innerHTML` / `beforeend` | Override swap strategy |
| `HX-Trigger` | `{"cartUpdated": {"count": 3}}` | Trigger client-side Alpine event |
| `HX-Push-Url` | `/orders?page=2` | Update browser URL bar |

### 2.3 HTMX Event Bus (via `HX-Trigger` header)

Server fires these events; Alpine.js listens on `window`:

```javascript
// Events server can trigger via HX-Trigger header
const ServerEvents = {
  CART_UPDATED:       'cartUpdated',       // { count: number, total: number }
  TOAST_SHOW:         'toastShow',         // { message: string, type: 'success'|'error'|'warning' }
  MODAL_CLOSE:        'modalClose',        // {}
  INVENTORY_LOW:      'inventoryLow',      // { variantId: string, remaining: number }
  SESSION_EXPIRING:   'sessionExpiring',   // { secondsLeft: number }
};
```

```csharp
// Server emits event via header (C# helper extension)
public static class HtmxResponseExtensions
{
    public static void HxTrigger(this HttpResponse response, string eventName, object? data = null)
    {
        var payload = data is null
            ? $"\"{eventName}\""
            : $"{{\"{eventName}\": {JsonSerializer.Serialize(data)}}}";
        response.Headers["HX-Trigger"] = payload;
    }
}

// Usage in PageModel
Response.HxTrigger(ServerEvents.CART_UPDATED, new { count = cart.ItemCount, total = cart.Total });
Response.HxTrigger(ServerEvents.TOAST_SHOW, new { message = "Added to cart!", type = "success" });
```

```javascript
// Alpine.js listener
window.addEventListener('cartUpdated', (e) => {
    Alpine.store('cart').count = e.detail.count;
    Alpine.store('cart').total = e.detail.total;
});
window.addEventListener('toastShow', (e) => {
    Alpine.store('toasts').add(e.detail.message, e.detail.type);
});
```

---

## 3. Alpine.js Global Stores

Defines the client-side state shape that backend HTML templates populate:

### 3.1 Cart Store
```javascript
// Alpine.js global store: initialised from server-rendered data attribute
Alpine.store('cart', {
    count: 0,           // Total item count (badge on nav)
    total: 0,           // Subtotal (display string, formatted server-side)
    reservations: [],   // [{ variantId, expiresAt (ISO8601) }]
    
    // Methods
    get nearestExpiry() {
        return this.reservations.sort((a,b) => a.expiresAt - b.expiresAt)[0]?.expiresAt;
    }
});
```

**Server initializes store on page load:**
```html
<!-- _Layout.cshtml — data from server, Alpine reads it -->
<div id="cart-init"
     x-data
     x-init="
       $store.cart.count = @Model.CartCount;
       $store.cart.total = @Model.CartTotal;
     "
     style="display:none">
</div>
```

### 3.2 Toast Store
```javascript
Alpine.store('toasts', {
    items: [],
    
    add(message, type = 'success', duration = 4000) {
        const id = Date.now();
        this.items.push({ id, message, type });
        setTimeout(() => this.remove(id), duration);
    },
    
    remove(id) {
        this.items = this.items.filter(t => t.id !== id);
    }
});
```

### 3.3 User Store
```javascript
Alpine.store('user', {
    isAuthenticated: false,
    id: null,
    name: '',
    role: '',           // 'buyer' | 'seller' | 'admin'
    
    get isSeller() { return this.role === 'seller'; },
    get isAdmin()  { return this.role === 'admin'; }
});
```

---

## 4. Form Field Naming Conventions

All form fields follow **PascalCase** matching C# model binding:

```html
<!-- Checkout form — matches CheckoutPageModel -->
<form method="post">
  @Html.AntiForgeryToken()
  <input name="ShippingAddress.Street" />
  <input name="ShippingAddress.City" />
  <input name="ShippingAddress.PostalCode" />
  <input name="PaymentMethod" value="CreditCard" />
</form>
```

### Validation Error Format (Server → HTML)
```html
<!-- Server returns partial with errors using ModelState -->
<div id="form-errors">
  <span asp-validation-for="ShippingAddress.Street" class="text-red-500"></span>
  <span asp-validation-for="ShippingAddress.City" class="text-red-500"></span>
</div>

<!-- Summary for non-field errors -->
<div asp-validation-summary="ModelOnly" class="text-red-500 text-sm bg-red-50 p-3 rounded"></div>
```

---

## 5. Partial View Naming Conventions

Partials returned by HTMX follow this pattern:

```
_                   ← Prefix for all partials
{Module}            ← Domain module
{ViewName}          ← What it shows
{Modifier}          ← Optional: Row, Card, Form, Empty, Error

Examples:
  _OrderRow.cshtml            ← Single order row in table
  _OrderStatusBadge.cshtml    ← Just the status badge
  _ProductCard.cshtml         ← Product grid card
  _ProductCardEmpty.cshtml    ← Empty state for product grid
  _CartSummary.cshtml         ← Cart count + mini preview
  _CartReservationTimer.cshtml ← TTL countdown timer
  _ReviewForm.cshtml          ← Review submission form
  _ReviewFormError.cshtml     ← Review form with validation errors
  _DisputeMessageThread.cshtml ← Dispute messages list
```

---

## 6. URL / Route Conventions

### Frontend Route Patterns (Razor Pages)
```
/                           → Pages/Index.cshtml
/search                     → Pages/Search/Index.cshtml
/shop/{slug}                → Pages/Shop/Index.cshtml
/shop/{slug}/products/{id}  → Pages/Shop/Products/Detail.cshtml
/cart                       → Pages/Cart/Index.cshtml
/checkout                   → Pages/Checkout/Index.cshtml
/orders/{id}/confirmation   → Pages/Orders/Confirmation.cshtml
/account/orders             → Pages/Account/Orders/Index.cshtml
/account/orders/{id}        → Pages/Account/Orders/Detail.cshtml
/seller/dashboard           → Pages/Seller/Dashboard.cshtml
/admin/disputes             → Pages/Admin/Disputes/Index.cshtml
```

### HTMX Partial Endpoints (separate handlers on same page)
```
GET  /cart/summary                     → returns _CartSummary partial
GET  /search/results?q=&page=          → returns _ProductGrid partial
POST /cart/items                       → add item, returns _CartSummary
DELETE /cart/items/{id}                → remove item, returns empty
GET  /seller/products?page=&status=    → returns _ProductList partial
POST /seller/variants/{id}/stock       → update stock, returns _StockCell
GET  /disputes/{id}/messages           → polling endpoint, returns _MessageList
```

---

## 7. Pagination Contract

All paginated lists use the same server-rendered pagination component:

```csharp
// Shared ViewModel used across all paginated lists
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
```

```html
<!-- _Pagination.cshtml: HTMX-compatible pagination -->
@model PagedResult<object>
<nav hx-boost="true">
  @if (Model.HasPreviousPage)
  {
    <a href="?page=@(Model.PageNumber - 1)" 
       hx-get="?page=@(Model.PageNumber - 1)"
       hx-target="#list-container">Previous</a>
  }
  <span>Page @Model.PageNumber of @Model.TotalPages</span>
  @if (Model.HasNextPage)
  {
    <a href="?page=@(Model.PageNumber + 1)"
       hx-get="?page=@(Model.PageNumber + 1)"
       hx-target="#list-container">Next</a>
  }
</nav>
```

---

## 8. Image Upload Contract

```html
<!-- Shared Alpine.js image upload component -->
<div x-data="imageUploader({ maxFiles: 3, maxSizeMb: 5, accept: 'image/jpeg,image/png,image/webp' })">
  <input type="file" 
         name="Images" 
         multiple 
         accept="image/jpeg,image/png,image/webp"
         @change="handleFiles($event)"
         class="hidden"
         x-ref="fileInput">
  
  <!-- Drop zone -->
  <div @click="$refs.fileInput.click()"
       @dragover.prevent="isDragging = true"
       @drop.prevent="handleDrop($event)"
       :class="isDragging ? 'border-brand-500' : 'border-gray-300'"
       class="border-2 border-dashed rounded-lg p-8 text-center cursor-pointer">
    <template x-if="previews.length === 0">
      <p>Drop images here or click to upload</p>
    </template>
    <div class="flex gap-2 flex-wrap">
      <template x-for="(preview, i) in previews">
        <div class="relative">
          <img :src="preview.url" class="w-24 h-24 object-cover rounded">
          <button @click.stop="removeFile(i)" class="absolute top-0 right-0">×</button>
        </div>
      </template>
    </div>
  </div>
  
  <!-- Hidden inputs for server binding -->
  <template x-for="(file, i) in files">
    <!-- File objects submitted with form -->
  </template>
  
  <p x-show="error" x-text="error" class="text-red-500 text-sm mt-1"></p>
</div>
```

**Server-side upload handler:**
```csharp
public async Task<IActionResult> OnPostUploadImageAsync(IFormFile[] images)
{
    const long maxBytes = 5 * 1024 * 1024; // 5MB
    var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
    
    foreach (var image in images)
    {
        if (image.Length > maxBytes)
            return BadRequest("Image too large");
        if (!allowedTypes.Contains(image.ContentType))
            return BadRequest("Invalid image type");
    }
    // Save to blob storage / wwwroot
}
```

---

## 9. Currency & Number Formatting

All monetary values:
- Stored as `decimal` with 2 decimal places in DB
- Formatted server-side (never client-side) using .NET culture
- Displayed as string in HTML (never raw numbers in FE logic)

```csharp
// Server formats currency — FE never does math on money
public static string FormatCurrency(decimal amount, string currency = "USD")
    => amount.ToString("C2", new CultureInfo("en-US")); // "$12.50"
```

```html
<!-- Never format currency in Alpine.js -->
<!-- ✅ Server provides formatted string -->
<span>@Model.Order.TotalFormatted</span>    <!-- "$125.00" -->

<!-- ❌ Don't do this -->
<span x-text="`$${total.toFixed(2)}`"></span>
```

---

## 10. Authentication State in HTML

Session / auth state flows from server to client via:

```html
<!-- _Layout.cshtml — embed auth state for Alpine store initialization -->
@if (User.Identity?.IsAuthenticated == true)
{
  <script>
    document.addEventListener('alpine:init', () => {
      Alpine.store('user', {
        isAuthenticated: true,
        id: '@User.GetUserId()',
        name: '@Html.Raw(Json.Serialize(User.GetName()))',
        role: '@User.GetRole()'
      });
    });
  </script>
}
```

**Security note**: Alpine.js `user` store is for **UX only** (show/hide menu items). All authorization decisions happen server-side. Never rely on `$store.user.role` for security.
